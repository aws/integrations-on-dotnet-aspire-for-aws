// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Microsoft.Extensions.Logging;

using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.AWS.CDK;
using Amazon.CDK.CXAPI;

namespace Aspire.Hosting.AWS.Environments;

public class CDKPublishingContext(string outputPath, ILambdaDeploymentPackager lambdaDeploymentPackager, ILogger logger)
{
    public async Task WriteModelAsync(DistributedApplicationModel model, AWSCDKEnvironment environment, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing to {output}", outputPath);
        ClearOutputDirectory();

        var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        if (File.Exists(tempFolder))
        {
            File.Delete(tempFolder);
        }

        var lambdaFunctions = model.Resources.OfType<LambdaProjectResource>().ToArray();
        foreach (var lambdaFunction in lambdaFunctions)
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
