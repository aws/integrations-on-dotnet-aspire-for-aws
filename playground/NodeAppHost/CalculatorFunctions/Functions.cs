using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CalculatorFunctions;

/// <summary>
/// Class library Lambda function model: each public method is a separate Lambda handler. The Aspire AppHost
/// registers each with a handler string of the form "Assembly::Namespace.Type::Method", for example
/// "CalculatorFunctions::CalculatorFunctions.Functions::Add". These are fronted by the API Gateway emulator.
/// </summary>
public class Functions
{
    /// <summary>
    /// Handles GET /add/{x}/{y} and returns the sum.
    /// </summary>
    public APIGatewayHttpApiV2ProxyResponse Add(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var (x, y) = ReadOperands(request);
        var result = x + y;
        context.Logger.LogInformation($"Adding {x} + {y} = {result}");
        return Ok(result);
    }

    /// <summary>
    /// Handles GET /subtract/{x}/{y} and returns the difference.
    /// </summary>
    public APIGatewayHttpApiV2ProxyResponse Subtract(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var (x, y) = ReadOperands(request);
        var result = x - y;
        context.Logger.LogInformation($"Subtracting {x} - {y} = {result}");
        return Ok(result);
    }

    private static (int X, int Y) ReadOperands(APIGatewayHttpApiV2ProxyRequest request)
    {
        var pathParameters = request.PathParameters ?? new Dictionary<string, string>();
        var x = int.Parse(pathParameters["x"]);
        var y = int.Parse(pathParameters["y"]);
        return (x, y);
    }

    private static APIGatewayHttpApiV2ProxyResponse Ok(int value) => new()
    {
        StatusCode = 200,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = value.ToString()
    };
}
