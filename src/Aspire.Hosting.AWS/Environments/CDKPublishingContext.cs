// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.CXAPI;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

public class CDKPublishingContext(string outputPath, ILambdaDeploymentPackager lambdaDeploymentPackager, ILogger logger)
{
    public async Task WriteModelAsync(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing to {output}", outputPath);
        ClearOutputDirectory();

        var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        if (File.Exists(tempFolder))
        {
            File.Delete(tempFolder);
        }

        var dotnetProjects = model.Resources.OfType<ProjectResource>().ToArray();

        
        foreach (var dotnetProject in dotnetProjects)
        {
            if (dotnetProject is LambdaProjectResource lambdaFunction)
            {
                var lambdaFunctionAnnotation = lambdaFunction.Annotations.OfType<LambdaFunctionAnnotation>().FirstOrDefault();

                logger.LogInformation("Creating deployment package for Lambda function '{LambdaFunctionName}'...", lambdaFunction.Name);
                var results = await lambdaDeploymentPackager.CreateDeploymentPackageAsync(lambdaFunction, tempFolder, cancellationToken);

                var functionProps = new FunctionProps
                {
                    Code = Code.FromAsset(results.LocalLocation!),
                    Handler = lambdaFunctionAnnotation!.Handler,
                    Runtime = Runtime.DOTNET_8,
                    MemorySize = 256,
                };
                foreach (var annotation in lambdaFunction.Annotations.OfType<PublishingCDKConfigureCallbackAnnotation>())
                {
                    if (annotation.LambdaFunctionPropsCallback == null)
                        continue;

                    annotation.LambdaFunctionPropsCallback(functionProps);
                }

                var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
                lambdaFunction.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = function });
                foreach (var annotation in lambdaFunction.Annotations.OfType<PublishingCDKConfigureCallbackAnnotation>())
                {
                    if (annotation.LambdaFunctionConstructCallback == null)
                        continue;

                    annotation.LambdaFunctionConstructCallback(function);
                }
            }
            else
            {
                var vpc = new Vpc(environment.CDKStack, "MyVpc", new VpcProps
                {
                    MaxAzs = 2 // Default is all AZs in the region
                });

                // Create an ECS cluster
                var cluster = new Cluster(environment.CDKStack, "MyEcsCluster", new ClusterProps
                {
                    Vpc = vpc
                });

                var fargateServiceProps = new ApplicationLoadBalancedFargateServiceProps
                {
                    Cluster = cluster,
                    Cpu = 256,
                    MemoryLimitMiB = 512,
                    DesiredCount = 2,
                    ListenerPort = 80,
                    PublicLoadBalancer = true,
                    MinHealthyPercent = 50,
                    TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
                    {
                        Image = ContainerImage.FromAsset(@"C:\codebase\integrations-on-dotnet-aspire-for-aws", new AssetImageProps
                        {
                            
                            File = "./playground/Publishing/Frontend/Dockerfile",
                        }),
                        ContainerPort = 80,
                    }
                };

                var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{dotnetProject.Name}", fargateServiceProps);
            }

        }

        var assembly = environment.CDKApp.Synth();
        MoveTempContentToOutput(assembly);
    }


    private void ClearOutputDirectory()
    {
        if (Directory.Exists(outputPath))
        {
            logger.LogTrace("Clearing output directory '{outputPath}'...", outputPath);
            foreach (var file in Directory.EnumerateFiles(outputPath))
            {
                logger.LogTrace("Deleting file '{file}'...", file);
                File.Delete(file);
            }
            foreach (var directory in Directory.EnumerateDirectories(outputPath))
            {
                logger.LogTrace("Deleting directory '{directory}'...", directory);
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private void MoveTempContentToOutput(CloudAssembly cloudAssembly)
    {
        foreach(var stack in cloudAssembly.Stacks)
        {
            var stackName = stack.StackName;
            var stackOutputPath = Path.Combine(outputPath, stackName);
            if (!Directory.Exists(stackOutputPath))
            {
                Directory.CreateDirectory(stackOutputPath);
            }

            Directory.CreateDirectory(stackOutputPath);
            foreach (var filePath in Directory.GetFiles(Directory.GetParent(stack.TemplateFullPath)!.FullName))
            {
                var fileName = Path.GetFileName(filePath);
                var destinationPath = Path.Combine(stackOutputPath, fileName);
                File.Move(filePath, destinationPath);
            }
        }
    }
}
