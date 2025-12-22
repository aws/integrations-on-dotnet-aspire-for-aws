// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001
using Amazon.CDK;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using static Amazon.CDK.AWS.ECS.CfnExpressGatewayService;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public class PublishCDKECSFargateExpressServiceConfig
    {
        public Action<CfnExpressGatewayServiceProps>? PropsCfnExpressGatewayServicePropsCallback { get; set; }

        public Action<CfnExpressGatewayService>? ConstructCfnExpressGatewayServiceCallback { get; set; }
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class PublishCDKECSFargateServiceExpressAnnotation : IResourceAnnotation
    {
        public PublishCDKECSFargateExpressServiceConfig Config { get; init; } = new PublishCDKECSFargateExpressServiceConfig();
    }
}

namespace Aspire.Hosting
{
    public static partial class AWSCDKEnvironmentExtensions
    {
        /// <summary>
        /// Deploy to AWS Elastic Container Service using the <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-overview.html">Express Mode</a>.
        /// Express mode deploys as an ECS service and a shared Application Load Balancer (ALB) across your Express mode services to route traffic to the service. 
        /// An HTTPS endpoint will be provisioned by default and a TargetGroup rule added to the ALB for the provisioned host name.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.CfnExpressGatewayService.html">CfnExpressGatewayService</a> construct is used to create the ECS Express Gateway service.
        /// </summary>
        /// <remarks>
        /// Port 8080 is assumed to be the container port the web application listens on. This can be customized by adding a callback on the config's PropsCfnExpressGatewayServicePropsCallback property.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
        public static IResourceBuilder<ProjectResource> PublishAsECSFargateExpressService(this IResourceBuilder<ProjectResource> builder, PublishCDKECSFargateExpressServiceConfig? config = null)
        {
            var annotation = new PublishCDKECSFargateServiceExpressAnnotation { Config = config ?? new PublishCDKECSFargateExpressServiceConfig() };
            builder.Resource.Annotations.Add(annotation);

            return builder;
        }
    }
}

namespace Aspire.Hosting.AWS.Environments.CDKResourceContexts
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class ECSFargateExpressServicePublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateExpressServicePublishTarget> logger) : AbstractAWSPublishTarget(logger)
    {
        public override string PublishTargetName => "ECS Fargate Express Service";

        public override Type PublishTargetAnnotation => typeof(PublishCDKECSFargateServiceExpressAnnotation);

        public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
        {
            var projectResource = resource as ProjectResource
                ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

            var publishAnnotation = annotation as PublishCDKECSFargateServiceExpressAnnotation
                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishCDKECSFargateServiceExpressAnnotation)}.");

            var imageTarballPath = await imageBuilder.BuildTarballImageAsync(projectResource, cancellationToken);

            var asset = new TarballImageAsset(environment.CDKStack, $"ContainerTarBall-{projectResource.Name}", new TarballImageAssetProps
            {
                TarballFile = imageTarballPath
            });

            var primaryContainer = new ExpressGatewayContainerProperty
            {
                Image = asset.ImageUri
            };

            var fargateServiceProps = new CfnExpressGatewayServiceProps
            {
                PrimaryContainer = primaryContainer,
                ServiceName = projectResource.Name
            };
            publishAnnotation.Config.PropsCfnExpressGatewayServicePropsCallback?.Invoke(fargateServiceProps);
            environment.DefaultValuesProvider.ApplyCfnExpressGatewayServiceDefaults(environment, fargateServiceProps);
            ProcessRelationShips(fargateServiceProps, projectResource);

            var fargateService = new CfnExpressGatewayService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
            publishAnnotation.Config.ConstructCfnExpressGatewayServiceCallback?.Invoke(fargateService);
            ApplyLinkedConstructAnnotation(projectResource, fargateService, this);

            _ = new CfnOutput(environment.CDKStack, "ExpressGatewayEndpoint", new CfnOutputProps
            {
                Description = "Endpoint for the ECS Express Gateway Service",
                Value = Fn.Join("", ["https://", Fn.GetAtt(fargateService.LogicalId, "Endpoint").ToString(), "/"])
            });
        }

        public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource)
        {
            if (resource is ProjectResource projectResource &&
                projectResource.GetEndpoints().Any() &&
                defaultProvider.DefaultWebAppPublishTarget == DefaultProvider.WebAppPublishTarget.ECSFargateExpressService
                )
            {
                return new IsDefaultPublishTargetMatchResult
                {
                    IsMatch = true,
                    PublishTargetAnnotation = new PublishCDKECSFargateServiceExpressAnnotation(),
                    Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 100 // Override to raise rank over console application default
                };
            }

            return IsDefaultPublishTargetMatchResult.NO_MATCH;
        }

        public override IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct)
        {
            if (resourceConstruct is not CfnExpressGatewayService fargateExpressConstruct)
                return null;

            var list = new List<KeyValuePair<string, string>>();

            var key = $"services__{resource.Name}__https__0";
            var endpoint = Fn.Join("", ["https://", Fn.GetAtt(fargateExpressConstruct.LogicalId, "Endpoint").ToString(), "/"]);
            list.Add(new KeyValuePair<string, string>(key, endpoint));

            return list.Any() ? list : null;
        }

        private void ProcessRelationShips(CfnExpressGatewayServiceProps props, ApplicationModel.IResource resource)
        {
            var dict = new Dictionary<string, string>();
            ApplyRelationshipEnvironmentVariable(dict, resource);

            if (!dict.Any())
                return;

            var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
            if (primaryContainer == null)
                throw new InvalidDataException("PrimaryContainer must be set in CfnExpressGatewayServiceProps and of type ExpressGatewayContainerProperty");

            var kvp = new List<IKeyValuePairProperty>();

            var existingKvp = primaryContainer.Environment as IKeyValuePairProperty[];
            if (existingKvp != null)
            {
                kvp.AddRange(existingKvp);
            }

            foreach (var item in dict)
            {
                kvp.Add(new KeyValuePairProperty
                {
                    Name = item.Key,
                    Value = item.Value
                });
            }

            primaryContainer.Environment = kvp.ToArray();
        }
    }
}