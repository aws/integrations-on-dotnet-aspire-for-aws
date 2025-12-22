// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public class PublishCDKECSFargateServiceConfig
    {
        public Action<ContainerDefinitionProps>? PropsContainerDefinitionCallback { get; set; }

        public Action<ContainerDefinition>? ConstructContainerDefinitionCallback { get; set; }

        public Action<FargateTaskDefinitionProps>? PropsFargateTaskDefinitionCallback { get; set; }

        public Action<FargateTaskDefinition>? ConstructFargateTaskDefinitionCallback { get; set; }

        public Action<FargateServiceProps>? PropsFargateServiceCallback { get; set; }

        public Action<FargateService>? ConstructFargateServiceCallback { get; set; }
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class PublishCDKECSFargateServiceAnnotation : IAWSPublishTargetAnnotation
    {
        public PublishCDKECSFargateServiceConfig Config { get; init; } = new PublishCDKECSFargateServiceConfig();
    }

}

namespace Aspire.Hosting
{
    public static partial class AWSCDKEnvironmentExtensions
    {
        /// <summary>
        /// Deploy to as a service to the AWS Elastic Container Service (ECS). An ECS service is a continuously running set of tasks running the console application as a container.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.FargateService.html">FargateService</a> construct is used to create the ECS service.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
        public static IResourceBuilder<ProjectResource> PublishAsECSFargateService(this IResourceBuilder<ProjectResource> builder, PublishCDKECSFargateServiceConfig? config = null)
        {
            var annotation = new PublishCDKECSFargateServiceAnnotation { Config = config ?? new PublishCDKECSFargateServiceConfig() };
            builder.Resource.Annotations.Add(annotation);

            return builder;
        }
    }
}

namespace Aspire.Hosting.AWS.Environments.CDKResourceContexts
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class ECSFargateServicePublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateServicePublishTarget> logger) : AbstractAWSPublishTarget(logger)
    {
        public override string PublishTargetName => "ECS Fargate";

        public override Type PublishTargetAnnotation => typeof(PublishCDKECSFargateServiceAnnotation);

        public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, ApplicationModel.IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
        {
            var projectResource = resource as ProjectResource
                ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

            var publishAnnotation = annotation as PublishCDKECSFargateServiceAnnotation
                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishCDKECSFargateServiceAnnotation)}.");

            var imageTarballPath = await imageBuilder.BuildTarballImageAsync(projectResource, cancellationToken);

            // Create Task Definition
            var fargateTaskDefinitionProps = new FargateTaskDefinitionProps();
            publishAnnotation.Config.PropsFargateTaskDefinitionCallback?.Invoke(fargateTaskDefinitionProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceDefaults(fargateTaskDefinitionProps);

            var taskDef = new FargateTaskDefinition(environment.CDKStack, $"TaskDefinition-{projectResource.Name}", fargateTaskDefinitionProps);
            publishAnnotation.Config.ConstructFargateTaskDefinitionCallback?.Invoke(taskDef);

            // Create Container Definition
            var containerDefinitionProps = new ContainerDefinitionProps
            {
                Image = ContainerImage.FromTarball(imageTarballPath),
                Environment = new Dictionary<string, string>()
            };
            ApplyRelationshipEnvironmentVariable(containerDefinitionProps.Environment, projectResource);
            publishAnnotation.Config.PropsContainerDefinitionCallback?.Invoke(containerDefinitionProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceDefaults(environment, projectResource.Name, containerDefinitionProps);

            var containerDefinition = taskDef.AddContainer($"Container-{projectResource.Name}", containerDefinitionProps);
            publishAnnotation.Config.ConstructContainerDefinitionCallback?.Invoke(containerDefinition);

            // Create Fargate Service
            var fargateServiceProps = new FargateServiceProps
            {
                TaskDefinition = taskDef,
            };
            publishAnnotation.Config.PropsFargateServiceCallback?.Invoke(fargateServiceProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceDefaults(environment, fargateServiceProps);

            var fargateService = new FargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
            publishAnnotation.Config.ConstructFargateServiceCallback?.Invoke(fargateService);
            ApplyLinkedConstructAnnotation(projectResource, fargateService, this);

            await ApplyDeploymentTagAsync(environment, projectResource, fargateService, cancellationToken);
        }

        public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource)
        {
            if (resource is ProjectResource &&
                defaultProvider.DefaultConsoleAppPublishTarget == DefaultProvider.ConsoleAppPublishTaret.ECSFargateService
                )
            {
                return new IsDefaultPublishTargetMatchResult
                {
                    IsMatch = true,
                    PublishTargetAnnotation = new PublishCDKECSFargateServiceAnnotation()
                };
            }

            return IsDefaultPublishTargetMatchResult.NO_MATCH;
        }

        public override IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct)
        {
            return null;
        }
    }
}