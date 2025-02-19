// // Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
//
// using Amazon;
// using Amazon.CloudFormation;
// using Amazon.CloudFormation.Model;
//
// using Aspire.Hosting.AWS.CloudFormation;
//
// namespace Aspire.Hosting.AWS.Integ.Tests;
//
// public class PlaygroundE2ETests
// {
//     [Fact]
//     public async Task RunAWSAppHostProject()
//     {
//         string? stackName = null;
//         IAmazonCloudFormation? cfClient = null;
//         try
//         {
//             var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AWS_AppHost>();
//             await using var app = await appHost.BuildAsync();
//             await app.StartAsync();
//
//             var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
//             await resourceNotificationService
//                 .WaitForResourceAsync("Frontend", KnownResourceStates.Running)
//                 .WaitAsync(TimeSpan.FromSeconds(120));
//
//             var cloudFormationResource = (ICloudFormationTemplateResource)appHost.Resources
//                                 .Single(static r => r.Name == "AspireSampleDevResources");
//
//             Assert.NotNull(cloudFormationResource.AWSSDKConfig);
//             cfClient = new AmazonCloudFormationClient(ConfigureServiceConfig(new AmazonCloudFormationConfig(), cloudFormationResource.AWSSDKConfig));
//
//             stackName = cloudFormationResource.StackName;
//             var stack = (await cfClient!.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName })).Stacks[0];
//             Assert.True(stack.StackStatus == StackStatus.CREATE_COMPLETE || stack.StackStatus == StackStatus.UPDATE_COMPLETE);
//
//             using var frontendClient = app.CreateHttpClient("Frontend");
//
//             Assert.Equal("\"Success\"", await frontendClient.GetStringAsync("/healthcheck/dynamodb"));
//             Assert.Equal("\"Success\"", await frontendClient.GetStringAsync("/healthcheck/cloudformation"));
//         }
//         finally
//         {
//             if (cfClient != null && stackName != null)
//             {
//                 await cfClient.DeleteStackAsync(new DeleteStackRequest { StackName = stackName });
//             }
//         }
//     }
//
//     private T ConfigureServiceConfig<T> (T sdkConfig, IAWSSDKConfig aspireConfig)
//         where T : Amazon.Runtime.ClientConfig
//     {
//         if (aspireConfig.Profile != null)
//         {
//             sdkConfig.Profile = new Profile(aspireConfig.Profile);
//         }
//
//         if (aspireConfig.Region != null)
//         {
//             sdkConfig.RegionEndpoint = aspireConfig.Region;
//         }
//
//         return sdkConfig;
//     }
// }
