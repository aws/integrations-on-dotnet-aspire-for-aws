// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.DynamoDB;

namespace Aspire.Hosting;

public static class DynamoDBLocalResourceBuilderExtensions
{
    /// <summary>
    /// Add an instance of DynamoDB local. This is a container pulled from Amazon ECR public gallery.
    /// Projects that use DynamoDB local can get a reference to the instance using the WithReference method.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">Optional: Options that can be set for configuring the instance of DynamoDB Local.</param>
    /// <returns></returns>
    /// <exception cref="DistributedApplicationException"></exception>
    public static IResourceBuilder<IDynamoDBLocalResource> AddAWSDynamoDBLocal(this IDistributedApplicationBuilder builder,
        string name, DynamoDBLocalOptions? options = null)
    {
        var container = new DynamoDBLocalResource(name, options ?? new DynamoDBLocalOptions());
        var containerBuilder = builder.AddResource(container)
                  .ExcludeFromManifest()
                  .WithEndpoint(targetPort: DynamoDBLocalResource.DynamoDBInternalPort, scheme: "http")
                  .WithArgs( container.CreateContainerImageArguments())
                  .WithImage(container.Options.Image, container.Options.Tag)
                  .WithImageRegistry(container.Options.Registry);

        if (!string.IsNullOrWhiteSpace(container.Options.LocalStorageDirectory))
        {
            containerBuilder.WithBindMount(container.Options.LocalStorageDirectory, DynamoDBLocalResource.InternalStorageMountPoint);
        }

        return containerBuilder;
    }

    /// <summary>
    /// Add a reference to the DynamoDB local to the project. This is done by setting the AWS_ENDPOINT_URL_DYNAMODB environment
    /// variable for the project to the http endpoint of the DynamoDB local container. 
    /// 
    /// When applications create the AmazonDynamoDBClient type, the service client for DynamoDB, and rely on the SDK to resolve the
    /// endpoint from the environment instead of explicitly setting an AWS region the SDK will use the value from 
    /// the AWS_ENDPOINT_URL_DYNAMODB environment variable.
    /// </summary>
    /// <typeparam name="TDestination"></typeparam>
    /// <param name="builder"></param>
    /// <param name="dynamoDBLocalResourceBuilder"></param>
    /// <returns></returns>
    /// <exception cref="DistributedApplicationException"></exception>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<IDynamoDBLocalResource> dynamoDBLocalResourceBuilder)
        where TDestination : IResourceWithEnvironment
    {      
        if (builder is IResourceBuilder<IResourceWithWaitSupport> waitSupport)
        {
            waitSupport.WaitFor(dynamoDBLocalResourceBuilder);
        }

        builder.WithEnvironment(context =>
        {
            if (context.ExecutionContext.IsPublishMode)
            {
                return;
            }

            var endpoint = dynamoDBLocalResourceBuilder.GetEndpoint("http");
            context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB"] = endpoint;
        });
        return builder;
    }
}