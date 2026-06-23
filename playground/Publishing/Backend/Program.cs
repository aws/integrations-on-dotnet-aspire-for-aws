using System.Text.Json;
using Amazon.BedrockAgentCore;
using Backend;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.AddRedisClient("cache");
builder.Services.AddHostedService<BackgroundProcessor>();
builder.Services.AddHttpClient<FrontendApiClient>(client =>
    {
        client.BaseAddress = new("https+http://Frontend");
    });

// The AWS SDK automatically picks up AWS_ENDPOINT_URL_BEDROCK_AGENTCORE
// set by Aspire's WithReference(horoscopeAgent) — no manual ServiceURL configuration needed.
builder.Services.TryAddAWSService<IAmazonBedrockAgentCore>();

// WithReference(horoscopeAgent) injects the agent's runtime ARN under this IConfiguration key.
// Locally it resolves to the "local-agent" placeholder (the emulator ignores it); when deployed
// the generated CDK stack sets it to the real AWS::BedrockAgentCore::Runtime ARN — same code path.
var agentRuntimeArn = builder.Configuration["AWS:Resources:HoroscopeAgent:AgentRuntimeArn"]
    ?? throw new InvalidOperationException(
        "Missing configuration 'AWS:Resources:HoroscopeAgent:AgentRuntimeArn'. " +
        "Ensure the backend has WithReference(horoscopeAgent) in the AppHost.");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => Results.Ok("Welcome to the backend! Try /horoscope/{sign} to get today's horoscope for a zodiac sign."));

app.MapGet("/horoscope/{sign}", async (string sign, IAmazonBedrockAgentCore agentClient) =>
{
    var payload = JsonSerializer.Serialize(new { prompt = $"Give me today's horoscope for {sign}." });
    using var payloadStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));

    var response = await agentClient.InvokeAgentRuntimeAsync(new Amazon.BedrockAgentCore.Model.InvokeAgentRuntimeRequest
    {
        AgentRuntimeArn = agentRuntimeArn,
        Payload = payloadStream,
        ContentType = "application/json"
    });

    using var reader = new StreamReader(response.Response);
    var body = await reader.ReadToEndAsync();

    return Results.Ok(new { sign, horoscope = body });
});

app.Run();
