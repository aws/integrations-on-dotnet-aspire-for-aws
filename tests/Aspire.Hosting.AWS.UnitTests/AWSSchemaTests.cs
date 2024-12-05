// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon;
using Json.Schema;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Aspire.Hosting.AWS.Tests;

public class AWSSchemaTests(ITestOutputHelper output)
{
    private static JsonSchema? s_schema;

    private static JsonSchema GetSchema()
    {
        if (s_schema == null)
        {
            var relativePath = Path.Combine("Schema", "aspire-8.0.json");
            var schemaPath = Path.GetFullPath(relativePath);
            s_schema = JsonSchema.FromFile(schemaPath);
        }

        return s_schema;
    }

    public static TheoryData<string, Action<IDistributedApplicationBuilder>> ApplicationSamples
    {
        get
        {
            var data = new TheoryData<string, Action<IDistributedApplicationBuilder>>
            {
                { "AwsStack", (IDistributedApplicationBuilder builder) =>
                    {
                        var awsSdkConfig = builder.AddAWSSDKConfig()
                                                  .WithRegion(RegionEndpoint.USWest2)
                                                  .WithProfile("test-profile");

                        builder.AddAWSCloudFormationStack("ExistingStack")
                               .WithReference(awsSdkConfig);
                    }
                },

                { "AwsTemplate", (IDistributedApplicationBuilder builder) =>
                    {
                        var awsSdkConfig = builder.AddAWSSDKConfig()
                                                  .WithRegion(RegionEndpoint.USWest2)
                                                  .WithProfile("test-profile");

                        builder.AddAWSCloudFormationTemplate("TemplateStack", "nonexistenttemplate")
                               .WithReference(awsSdkConfig);
                    }
                }
            };

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ApplicationSamples))]
    public async Task ValidateApplicationSamples(string testCaseName, Action<IDistributedApplicationBuilder> configurator)
    {
        var tempDir = Directory.CreateTempSubdirectory(testCaseName);
        var outputPath = Path.Combine(tempDir.FullName, "aspire-manifest.json");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputPath]);
        configurator(builder);

        using var program = builder.Build();
        program.Run();

        var manifestText = await File.ReadAllTextAsync(outputPath);
        AssertValid(manifestText);

        try
        {
            tempDir.Delete(recursive: true);
        }
        catch (IOException ex)
        {
            output.WriteLine($"Failed to delete {tempDir.FullName} : {ex.Message}. Ignoring.");
        }
    }

    private static void AssertValid(string manifestText)
    {
        var manifestJson = JsonNode.Parse(manifestText);
        var schema = GetSchema();
        var results = schema.Evaluate(manifestJson);

        if (!results.IsValid)
        {
            var errorMessages = results.Details.Where(x => x.HasErrors).SelectMany(e => e.Errors!).Select(e => e.Value);
            Assert.True(results.IsValid, string.Join(Environment.NewLine, errorMessages ?? ["Schema failed validation with no errors"]));
        }
    }
}
