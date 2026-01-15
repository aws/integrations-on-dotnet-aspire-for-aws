// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.SecretsManager;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.CDK;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding AWS Secrets Manager resources to the application model.
/// </summary>
public static class SecretsManagerResourceExtensions
{
    private const string SecretArnOutputName = "SecretArn";
    private const string SecretNameOutputName = "SecretName";

    /// <summary>
    /// Adds an AWS Secrets Manager secret.
    /// </summary>
    /// <param name="builder">The builder for the AWS CDK stack.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="props">The properties of the secret.</param>
    /// <returns>A resource builder for the secret.</returns>
    public static IResourceBuilder<IConstructResource<Secret>> AddSecret(this IResourceBuilder<IStackResource> builder, [ResourceName] string name, ISecretProps? props = null)
    {
        return builder.AddConstruct(name, scope => new Secret(scope, name, props));
    }

    /// <summary>
    /// Adds an AWS Secrets Manager secret with a generated string value.
    /// </summary>
    /// <param name="builder">The builder for the AWS CDK stack.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="generateSecretString">Configuration for generating the secret string.</param>
    /// <param name="description">Optional description for the secret.</param>
    /// <returns>A resource builder for the secret.</returns>
    public static IResourceBuilder<IConstructResource<Secret>> AddGeneratedSecret(
        this IResourceBuilder<IStackResource> builder, 
        [ResourceName] string name, 
        SecretStringGenerator generateSecretString,
        string? description = null)
    {
        var props = new SecretProps
        {
            GenerateSecretString = generateSecretString,
            Description = description
        };

        return builder.AddConstruct(name, scope => new Secret(scope, name, props));
    }

    /// <summary>
    /// Adds a reference of an AWS Secrets Manager secret to a project. The output parameters of the secret are added to the project IConfiguration.
    /// </summary>
    /// <param name="builder">The builder for the resource.</param>
    /// <param name="secret">The AWS Secrets Manager secret resource.</param>
    /// <param name="configSection">The optional config section in IConfiguration to add the output parameters.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, 
        IResourceBuilder<IConstructResource<Secret>> secret, 
        string? configSection = null)
        where TDestination : IResourceWithEnvironment
    {
        configSection ??= $"{Constants.DefaultConfigSection}:{secret.Resource.Name}";
        var prefix = configSection.ToEnvironmentVariables();
        
        builder.WithEnvironment($"{prefix}__{SecretArnOutputName}", secret, s => s.SecretArn, SecretArnOutputName);
        builder.WithEnvironment($"{prefix}__{SecretNameOutputName}", secret, s => s.SecretName, SecretNameOutputName);
        
        return builder;
    }

    /// <summary>
    /// Adds a reference to an AWS Secrets Manager secret with a specific environment variable name for the secret ARN.
    /// </summary>
    /// <param name="builder">The builder for the resource.</param>
    /// <param name="secret">The AWS Secrets Manager secret resource.</param>
    /// <param name="envVarName">The environment variable name to use for the secret ARN.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<TDestination> WithSecretReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<IConstructResource<Secret>> secret,
        string envVarName)
        where TDestination : IResourceWithEnvironment
    {
        return builder.WithEnvironment(envVarName, secret, s => s.SecretArn, SecretArnOutputName);
    }
}
