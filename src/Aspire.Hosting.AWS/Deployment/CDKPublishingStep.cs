// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.Deployment.CDKPublishTargets;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class CDKPublishingStep(IServiceProvider serviceProvider, ILogger<CDKPublishingStep> logger, IAWSEnvironmentService environmentService)
{
    IDictionary<Type, IAWSPublishTarget> _annotationsToPublishTargetsMapping = new Dictionary<Type, IAWSPublishTarget>(); 

    public async Task GenerateCDKOutputAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        var step = await context.ReportingStep.CreateTaskAsync($"Synthesizing CDK Application", cancellationToken);
        try
        {
            logger.LogDebug("Starting synthesis of CDK application for environment {EnvironmentName}", environment.Name);

            environment.InitializeCDKApp(DetermineOutputDirectory());
            InitializePublishTargetMapping();
            logger.LogDebug("Capture of output from CDK context generation:\n{CdkContextLog}", environment.CDKContextGenerationLog);

            var outputPath = environment.CDKApp.Outdir;

            logger.LogInformation("Publishing to {Output}", outputPath);
            ClearOutputDirectory(outputPath);

            ApplyDefaultPublishTargetAnnotations(model, environment);
            await ProcessResources(context, model, environment, cancellationToken);

            environment.CDKApp.Synth();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !environment.Config.DisablePlatformCorrection)
            {
                FixCDKAssetsFileForWindows(outputPath);
            }

            await step.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synthesize CDK application");

            if (environment.CDKContextGenerationLog == null)
            {
                logger.LogDebug("No CDK context generation was run before synthesizing the application");
            }
            else
            {
                logger.LogDebug("Console out from generating the CDK context used to synthesize the application:\n{CdkContextLog}", environment.CDKContextGenerationLog);
            }

            await step.FailAsync($"Failed to synthesize CDK application: {ex}", cancellationToken);
        }
    }

    /// <summary>
    /// We have to do our own searching through the commandline arguments parameters to find the output path because
    /// we need it for creating the CDK app which might happen before the Aspire publish pipeline is created. It is
    /// a quirk of CDK that the output path has to be set on the app itself.
    /// </summary>
    /// <returns></returns>
    private string DetermineOutputDirectory()
    {
        string? outputPath = null;
        var args = environmentService.GetCommandLineArgs();

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--output-path", StringComparison.CurrentCultureIgnoreCase) || string.Equals(args[i], "-o", StringComparison.CurrentCultureIgnoreCase))
            {
                outputPath = args[i + 1];
            }
        }

        if (outputPath == null)
        {
            outputPath = Environment.CurrentDirectory;
        }

        if (!string.Equals(new DirectoryInfo(outputPath).Name, "cdk.out"))
        {
            outputPath = Path.Combine(outputPath, "cdk.out");
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        return outputPath;
    }

    private void InitializePublishTargetMapping()
    {
        _annotationsToPublishTargetsMapping.Clear();

        var awsPublishingTargets = serviceProvider.GetServices<IAWSPublishTarget>();
        foreach (var publishingTarget in awsPublishingTargets)
        {
            logger.LogTrace("Registering AWS CDK publishing target {PublishTargetName} for annotation {PublishTargetAnnotation}", publishingTarget.GetType().Name, publishingTarget.PublishTargetAnnotation.Name);
            _annotationsToPublishTargetsMapping[publishingTarget.PublishTargetAnnotation] = publishingTarget;
        }
    }

    private void ApplyDefaultPublishTargetAnnotations(DistributedApplicationModel model, AWSCDKEnvironmentResource environment)
    {
        foreach (var resource in model.Resources)
        {
            if (resource.IsExcludedFromPublish())
                continue;

            if (!resource.TryGetLastAnnotation<IAWSPublishTargetAnnotation>(out _))
            {
                var annotation = DetermineDefaultPublishAnnotation(environment, resource);
                if (annotation == null)
                {
                    if (resource is not AWSCDKEnvironmentResource &&
                        resource is not StackResource && 
                        resource is not ParameterResource)
                    {
                        logger.LogInformation("Resource \"{ResourceName}\" of type \"{ResourceType}\" has no AWS publish target and will not be included in CDK stack", resource.Name, resource.GetType().Namespace + "." + resource.GetType().Name);
                    }
                    continue;
                }

                resource.Annotations.Add(annotation);
            }
        }
    }

    private IResourceAnnotation? DetermineDefaultPublishAnnotation(AWSCDKEnvironmentResource environment, IResource resource)
    {
        IsDefaultPublishTargetMatchResult? bestMatch = null;
        foreach(var publishTarget in _annotationsToPublishTargetsMapping.Values)
        {
            var matchResults = publishTarget.IsDefaultPublishTargetMatch(environment.DefaultsProvider, resource);

            if (matchResults.IsMatch && (bestMatch == null || bestMatch.Rank < matchResults.Rank))
            {
                bestMatch = matchResults;
            }
        }

        return bestMatch?.PublishTargetAnnotation;
    }

    private async Task ProcessResources(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        // TODO: Make sure the order of processing resources responds to dependencies between resources
        foreach (var resource in model.Resources)
        {
            if (resource.IsExcludedFromPublish())
                continue;

            if (!resource.TryGetLastAnnotation<IAWSPublishTargetAnnotation>(out var publishAnnotation))
                continue;

            if (!_annotationsToPublishTargetsMapping.TryGetValue(publishAnnotation.GetType(), out var publishTarget))
                continue;

            var activityTask = await context.ReportingStep.CreateTaskAsync($"Preparing {resource.Name} for {publishTarget.PublishTargetName}", cancellationToken);
            try
            {
                await publishTarget.GenerateConstructAsync(environment, resource, publishAnnotation, cancellationToken);
                await activityTask.SucceedAsync(cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to prepare {ProjectName} for {ResourceType}", resource.Name, publishTarget.PublishTargetName);
                await activityTask.FailAsync($"Failed to prepare {resource.Name} for {publishTarget.PublishTargetName}: {ex}", cancellationToken);
                throw;
            }
        }
    }

    private void ClearOutputDirectory(string outputPath)
    {
        if (Directory.Exists(outputPath))
        {
            logger.LogTrace("Clearing output directory '{OutputPath}'...", outputPath);
            foreach (var file in Directory.EnumerateFiles(outputPath))
            {
                logger.LogTrace("Deleting file '{File}'...", file);
                File.Delete(file);
            }
            foreach (var directory in Directory.EnumerateDirectories(outputPath))
            {
                logger.LogTrace("Deleting directory '{Directory}'...", directory);
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private void FixCDKAssetsFileForWindows(string outputPath)
    {
        foreach (var file in Directory.EnumerateFiles(outputPath, "*.assets.json", SearchOption.AllDirectories))
        {
            try
            {
                string json = File.ReadAllText(file);
                JsonNode root = JsonNode.Parse(json)!;
                bool changed = false;

                if (root["dockerImages"] is JsonObject dockerImages)
                {
                    foreach (var image in dockerImages)
                    {
                        JsonObject imageObject = image.Value!.AsObject();

                        if (imageObject["source"]?["executable"] is JsonArray)
                        {
                            // Replace the executable array
                            imageObject["source"]!["executable"] = new JsonArray("powershell", "-Command", $"docker load -i asset.{image.Key}.tar | ForEach-Object {{ ($_ -replace '^Loaded image: ', '') }}");
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    var updatedJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(file, updatedJson);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error switching CDK assets file {File} to use PowerShell for restoring tarball", file);
            }
        }
    }
}
