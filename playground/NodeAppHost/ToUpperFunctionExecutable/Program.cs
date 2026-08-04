using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

// Executable Lambda function model: the project is its own executable that hosts the Lambda runtime loop.
// The Aspire AppHost registers this with the handler set to the assembly name "ToUpperFunctionExecutable" and
// fronts it with the API Gateway emulator at POST /toupper, so the handler receives an API Gateway request and
// returns an API Gateway response.

// The AmazonDynamoDBClient is created without an explicit endpoint or region. Because the AppHost wires this
// function with withDynamoDBLocalReference, the AWS_ENDPOINT_URL_DYNAMODB environment variable is injected and
// the SDK automatically targets the local DynamoDB container instead of the real DynamoDB service.
var dynamoDBClient = new AmazonDynamoDBClient();

const string TableName = "ToUpperCounts";
var tableReady = false;

var handler = async (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
{
    // The request body is the string to convert (sent as a JSON string, e.g. "hello", or plain text).
    var input = (request.Body ?? string.Empty).Trim().Trim('"');
    context.Logger.LogInformation($"Converting '{input}' to upper case. DynamoDB endpoint: {Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL_DYNAMODB")}");

    await EnsureTableExistsAsync(context);

    var upper = input.ToUpperInvariant();

    // Atomically increment the counter for this input string. The input is the hash key, and the ADD update
    // expression creates the item with Count = 1 on first use and increments it on each subsequent use.
    var updateResponse = await dynamoDBClient.UpdateItemAsync(new UpdateItemRequest
    {
        TableName = TableName,
        Key = new Dictionary<string, AttributeValue> { ["Input"] = new AttributeValue { S = input } },
        UpdateExpression = "ADD #count :one",
        ExpressionAttributeNames = new Dictionary<string, string> { ["#count"] = "Count" },
        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":one"] = new AttributeValue { N = "1" } },
        ReturnValues = ReturnValue.UPDATED_NEW
    });

    var count = updateResponse.Attributes["Count"].N;
    context.Logger.LogInformation($"'{input}' has now been converted {count} time(s).");

    return new APIGatewayHttpApiV2ProxyResponse
    {
        StatusCode = 200,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = $"{upper} (converted {count} time(s))"
    };
};

await LambdaBootstrapBuilder.Create<APIGatewayHttpApiV2ProxyRequest, APIGatewayHttpApiV2ProxyResponse>(handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();

// Creates the counter table on first invocation if it does not already exist. DynamoDB Local starts empty on
// each run (unless configured with persistent storage), so the function creates its own schema.
async Task EnsureTableExistsAsync(ILambdaContext context)
{
    if (tableReady)
    {
        return;
    }

    var tables = await dynamoDBClient.ListTablesAsync();
    if (!tables.TableNames.Contains(TableName))
    {
        context.Logger.LogInformation($"Creating DynamoDB table '{TableName}'.");
        await dynamoDBClient.CreateTableAsync(new CreateTableRequest
        {
            TableName = TableName,
            AttributeDefinitions = [new AttributeDefinition("Input", ScalarAttributeType.S)],
            KeySchema = [new KeySchemaElement("Input", KeyType.HASH)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        // Wait until the table becomes ACTIVE before using it.
        while (true)
        {
            var describe = await dynamoDBClient.DescribeTableAsync(TableName);
            if (describe.Table.TableStatus == TableStatus.ACTIVE)
            {
                break;
            }
            await Task.Delay(500);
        }
    }

    tableReady = true;
}
