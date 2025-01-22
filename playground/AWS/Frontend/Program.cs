// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Frontend.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAWSService<IAmazonDynamoDB>(); 
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();

// Configuring messaging using the AWS.Messaging library.
builder.Services.AddAWSMessageBus(messageBuilder =>
{
    // Get the SQS queue URL that was created from AppHost and assigned to the project.
    var chatTopicArn = builder.Configuration["AWS:Resources:ChatTopicArn"];
    if (chatTopicArn != null)
    {
        messageBuilder.AddSNSPublisher<Frontend.Models.ChatMessage>(chatTopicArn);
    }
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/healthcheck/dynamodb", (HttpContext ctx) =>
{
    var ddbClient = app.Services.GetRequiredService<IAmazonDynamoDB>();
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL_DYNAMODB")))
    {
        return Results.BadRequest("The AWS_ENDPOINT_URL_DYNAMODB is not set");
    }
    if (!ddbClient.Config.ServiceURL.StartsWith(Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL_DYNAMODB")!))
    {
        return Results.BadRequest("The DynamoDB service client is not configured for DyanamoDB local");
    }

    return Results.Ok("Success");
});


app.MapGet("/healthcheck/cloudformation", (HttpContext ctx) =>
{
    // Confirm the WithEnvironment behavior
    if (builder.Configuration["ChatTopicArnEnv"] == null)
    {
        return Results.BadRequest("Missing ChatTopicArnEnv");
    }

    // Confirm the WithReference behavior
    if (builder.Configuration["AWS:Resources:ChatTopicArn"] == null)
    {
        return Results.BadRequest("Missing ChatTopicArn");
    }
    if (builder.Configuration["AWS:Resources:ChatMessagesQueueUrl"] == null)
    {
        return Results.BadRequest("Missing ChatTopicArn");
    }

    return Results.Ok("Success");
});


app.Run();
