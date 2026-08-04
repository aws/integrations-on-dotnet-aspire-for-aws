// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.IO;
using System.Linq;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

/// <summary>
/// Tests for the polyglot (non-.NET AppHost) Lambda entry point. These cover the project path resolution and
/// build behavior that differ from the generic <c>AddAWSLambdaFunction&lt;TLambdaProject&gt;</c> overload, which
/// receives generated Projects metadata rather than a path string.
/// </summary>
public class LambdaPolyglotTests
{
    [Fact]
    public void AddAWSLambdaFunctionForPolyglot_ResolvesDirectoryToCsproj_AndDoesNotSuppressBuild()
    {
        // Arrange: a directory containing a single .csproj, referenced with a path relative to the AppHost directory.
        var appHostDir = Directory.GetCurrentDirectory();
        var projectDirName = "PolyglotLambdaProject_" + Path.GetRandomFileName();
        var projectDir = Path.Combine(appHostDir, projectDirName);
        Directory.CreateDirectory(projectDir);
        var expectedCsproj = Path.Combine(projectDir, "PolyglotLambdaProject.csproj");
        File.WriteAllText(expectedCsproj, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var builder = DistributedApplication.CreateBuilder();

            // Act: use the relative directory path, as a TypeScript AppHost would.
            var lambda = builder.AddAWSLambdaFunctionForPolyglot("DynamoDBFunction", "./" + projectDirName, "PolyglotLambdaProject");

            var metadata = lambda.Resource.Annotations.OfType<IProjectMetadata>().Single();

            // Assert: the directory was resolved to the single .csproj it contains.
            Assert.Equal(expectedCsproj, metadata.ProjectPath);

            // Assert: the project is built as part of `dotnet run` (SuppressBuild must be false). A true value would
            // pass --no-build and fail because a polyglot project is never pre-built, unlike the class library wrapper.
            Assert.False(metadata.SuppressBuild);
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Fact]
    public void AddAWSLambdaFunctionForPolyglot_AcceptsDirectCsprojPath()
    {
        var appHostDir = Directory.GetCurrentDirectory();
        var projectDir = Path.Combine(appHostDir, "PolyglotLambdaProjectFile_" + Path.GetRandomFileName());
        Directory.CreateDirectory(projectDir);
        var csproj = Path.Combine(projectDir, "MyLambda.csproj");
        File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var builder = DistributedApplication.CreateBuilder();

            // A rooted path directly to the .csproj file should be used as-is.
            var lambda = builder.AddAWSLambdaFunctionForPolyglot("DynamoDBFunction", csproj, "MyLambda");

            var metadata = lambda.Resource.Annotations.OfType<IProjectMetadata>().Single();
            Assert.Equal(csproj, metadata.ProjectPath);
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Fact]
    public void AddAWSLambdaFunctionForPolyglot_MapsLogOptionStringsToSdkValues()
    {
        var appHostDir = Directory.GetCurrentDirectory();
        var projectDir = Path.Combine(appHostDir, "PolyglotLambdaOptions_" + Path.GetRandomFileName());
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "OptLambda.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var builder = DistributedApplication.CreateBuilder();

            // Should not throw when mapping the string log options to the AWS SDK ConstantClass values.
            var lambda = builder.AddAWSLambdaFunctionForPolyglot(
                "DynamoDBFunction",
                projectDir,
                "OptLambda",
                new LambdaFunctionPolyglotOptions { LogFormat = "JSON", ApplicationLogLevel = "DEBUG" });

            Assert.NotNull(lambda.Resource.Annotations.OfType<IProjectMetadata>().Single());
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }
}
