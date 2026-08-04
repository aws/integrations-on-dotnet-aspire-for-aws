// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Linq;
using System.Reflection;
using Amazon;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.DynamoDB;
using Aspire.Hosting.AWS.Lambda;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

/// <summary>
/// Verifies the ATS (Aspire Type System) exports that make the AWS SDK config and DynamoDB Local
/// features available to TypeScript/node AppHosts. These assertions guard the attribute presence and
/// the pinned capability ids that the TypeScript SDK generator relies on. The explicit capability ids
/// (e.g. "withDynamoDBLocalReference") must stay stable because they become the generated method names
/// that TypeScript AppHost authors call; auto-derived names could silently rename on future additions.
/// </summary>
public class AtsExportTests
{
    [Fact]
    public void AddAWSSDKConfig_IsExportedCapability()
    {
        var method = GetMethod(typeof(SDKResourceExtensions), nameof(SDKResourceExtensions.AddAWSSDKConfig));
        Assert.NotNull(GetAspireExport(method));
    }

    [Fact]
    public void WithProfile_IsExportedCapability()
    {
        var method = GetMethod(typeof(SDKResourceExtensions), nameof(SDKResourceExtensions.WithProfile));
        Assert.NotNull(GetAspireExport(method));
    }

    [Fact]
    public void WithSdkValidation_IsExportedCapability()
    {
        var method = GetMethod(typeof(SDKResourceExtensions), nameof(SDKResourceExtensions.WithSdkValidation));
        Assert.NotNull(GetAspireExport(method));
    }

    [Fact]
    public void WithRegion_StringOverload_IsExported_And_RegionEndpointOverloadIsNot()
    {
        var stringOverload = typeof(SDKResourceExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(SDKResourceExtensions.WithRegion)
                         && m.GetParameters().Any(p => p.ParameterType == typeof(string)));
        var stringExport = GetAspireExport(stringOverload);
        Assert.NotNull(stringExport);
        // The TS-facing name is pinned to "withRegion".
        Assert.Equal("withRegion", GetExportId(stringExport!));

        // The RegionEndpoint overload has no clean TypeScript representation and must remain C#-only.
        var regionEndpointOverload = typeof(SDKResourceExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(SDKResourceExtensions.WithRegion)
                         && m.GetParameters().Any(p => p.ParameterType == typeof(RegionEndpoint)));
        Assert.Null(GetAspireExport(regionEndpointOverload));
    }

    [Fact]
    public void SDKConfigWithReference_IsExported_WithPinnedId()
    {
        var method = typeof(SDKResourceExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(SDKResourceExtensions.WithReference));
        var export = GetAspireExport(method);
        Assert.NotNull(export);
        Assert.Equal("withAWSSDKConfigReference", GetExportId(export!));
    }

    [Fact]
    public void IAWSSDKConfig_IsExportedHandleType()
    {
        Assert.NotNull(GetAspireExport(typeof(IAWSSDKConfig)));
    }

    [Fact]
    public void AddAWSDynamoDBLocal_IsExportedCapability()
    {
        var method = GetMethod(typeof(DynamoDBLocalResourceBuilderExtensions), nameof(DynamoDBLocalResourceBuilderExtensions.AddAWSDynamoDBLocal));
        Assert.NotNull(GetAspireExport(method));
    }

    [Fact]
    public void DynamoDBLocalWithReference_IsExported_WithPinnedId()
    {
        var method = typeof(DynamoDBLocalResourceBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(DynamoDBLocalResourceBuilderExtensions.WithReference));
        var export = GetAspireExport(method);
        Assert.NotNull(export);
        Assert.Equal("withDynamoDBLocalReference", GetExportId(export!));
    }

    [Fact]
    public void DynamoDBLocalResource_IsExportedHandleType()
    {
        Assert.NotNull(GetAspireExport(typeof(DynamoDBLocalResource)));
    }

    [Fact]
    public void DynamoDBLocalOptions_IsExportedDto()
    {
        Assert.True(HasAspireDto(typeof(DynamoDBLocalOptions)));
    }

    [Fact]
    public void AddAWSLambdaFunctionForPolyglot_IsExported_WithPinnedId()
    {
        // The generic AddAWSLambdaFunction<T> cannot be called from TypeScript, so a path-based
        // polyglot entry point is exported instead under the pinned name "addAWSLambdaFunction".
        var method = typeof(LambdaExtensions)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "AddAWSLambdaFunctionForPolyglot");
        var export = GetAspireExport(method);
        Assert.NotNull(export);
        Assert.Equal("addAWSLambdaFunction", GetExportId(export!));
    }

    [Fact]
    public void GenericAddAWSLambdaFunction_IsNotExported()
    {
        // The generic .NET overload must remain unexported; only the polyglot overload is exposed to TypeScript.
        var method = typeof(LambdaExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(LambdaExtensions.AddAWSLambdaFunction));
        Assert.Null(GetAspireExport(method));
    }

    [Fact]
    public void LambdaFunctionPolyglotOptions_IsInternalExportedDto()
    {
        // The polyglot options DTO is internal so .NET AppHosts keep using LambdaFunctionOptions
        // while only the generated TypeScript SDK sees the string-based shape.
        var type = typeof(LambdaExtensions).Assembly
            .GetType("Aspire.Hosting.AWS.Lambda.LambdaFunctionPolyglotOptions");
        Assert.NotNull(type);
        Assert.False(type!.IsPublic);
        Assert.True(HasAspireDto(type));
    }

    [Fact]
    public void AddAWSLambdaServiceEmulator_IsExportedCapability()
    {
        var method = GetMethod(typeof(LambdaExtensions), nameof(LambdaExtensions.AddAWSLambdaServiceEmulator));
        Assert.NotNull(GetAspireExport(method));
    }

    [Fact]
    public void AddAWSAPIGatewayEmulator_IsExportedCapability()
    {
        var method = GetMethod(typeof(APIGatewayExtensions), nameof(APIGatewayExtensions.AddAWSAPIGatewayEmulator));
        Assert.NotNull(GetAspireExport(method));
    }

    [Fact]
    public void APIGatewayWithReference_IsExported_WithPinnedId()
    {
        var method = typeof(APIGatewayExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(APIGatewayExtensions.WithReference));
        var export = GetAspireExport(method);
        Assert.NotNull(export);
        Assert.Equal("withAPIGatewayLambdaReference", GetExportId(export!));
    }

    [Fact]
    public void SQSEventSource_StringOverloadExported_ConstructAndCfnOverloadsNot()
    {
        var overloads = typeof(SQSEventSourceExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(SQSEventSourceExtensions.WithSQSEventSource))
            .ToList();

        var stringOverload = overloads.Single(m => m.GetParameters()[1].ParameterType == typeof(string));
        Assert.Equal("withSQSEventSource", GetExportId(GetAspireExport(stringOverload)!));

        // The CDK-construct and CloudFormation overloads depend on types not available to TypeScript and must not be exported.
        foreach (var other in overloads.Where(m => m.GetParameters()[1].ParameterType != typeof(string)))
        {
            Assert.Null(GetAspireExport(other));
        }
    }

    [Fact]
    public void DynamoDBStreamsEventSource_StringOverloadExported_ConstructAndCfnOverloadsNot()
    {
        var overloads = typeof(DynamoDBStreamsEventSourceExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(DynamoDBStreamsEventSourceExtensions.WithDynamoDBStreamsEventSource))
            .ToList();

        var stringOverload = overloads.Single(m => m.GetParameters()[1].ParameterType == typeof(string));
        Assert.Equal("withDynamoDBStreamsEventSource", GetExportId(GetAspireExport(stringOverload)!));

        foreach (var other in overloads.Where(m => m.GetParameters()[1].ParameterType != typeof(string)))
        {
            Assert.Null(GetAspireExport(other));
        }
    }

    [Theory]
    [InlineData(typeof(LambdaProjectResource))]
    [InlineData(typeof(LambdaEmulatorResource))]
    [InlineData(typeof(APIGatewayEmulatorResource))]
    public void LambdaResourceTypes_AreExportedHandleTypes(System.Type type)
    {
        Assert.NotNull(GetAspireExport(type));
    }

    [Theory]
    [InlineData(typeof(LambdaEmulatorOptions))]
    [InlineData(typeof(APIGatewayEmulatorOptions))]
    [InlineData(typeof(SQSEventSourceOptions))]
    [InlineData(typeof(DynamoDBStreamsEventSourceOptions))]
    public void LambdaOptionTypes_AreExportedDtos(System.Type type)
    {
        Assert.True(HasAspireDto(type));
    }

    private static MethodInfo GetMethod(System.Type type, string name)
        => type.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == name);

    private static System.Attribute? GetAspireExport(MemberInfo member)
        => member.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().FullName == "Aspire.Hosting.AspireExportAttribute");

    private static bool HasAspireDto(System.Type type)
        => type.GetCustomAttributes()
            .Any(a => a.GetType().FullName == "Aspire.Hosting.AspireDtoAttribute");

    /// <summary>
    /// Reads the explicit capability id (the string passed to <c>[AspireExport("...")]</c>) via reflection
    /// so the test does not need a compile-time reference to the attribute's Id property.
    /// </summary>
    private static string? GetExportId(System.Attribute export)
        => export.GetType().GetProperty("Id")?.GetValue(export) as string;
}
