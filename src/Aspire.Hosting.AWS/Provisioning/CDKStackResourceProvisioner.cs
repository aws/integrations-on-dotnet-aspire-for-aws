// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.CXAPI;
using Amazon.CloudFormation;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Provisioning;

internal sealed class CDKStackResourceProvisioner(
    ResourceLoggerService loggerService,
    ResourceNotificationService notificationService)
    : CloudFormationTemplateResourceProvisioner<StackResource>(loggerService, notificationService)
{
    protected override async Task GetOrCreateResourceAsync(StackResource resource, CancellationToken cancellationToken)
    {
        var logger = LoggerService.GetLogger(resource);
        await ProvisionCDKStackAssetsAsync(resource, logger, cancellationToken).ConfigureAwait(false);
        await base.GetOrCreateResourceAsync(resource, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ProvisionCDKStackAssetsAsync(StackResource resource, ILogger logger, CancellationToken cancellationToken)
    {
        if (!resource.TryGetStackArtifact(out var artifact))
        {
            throw new AWSProvisioningException("Failed to provision stack assets. Could not retrieve stack artifact.");
        }

        var assetArtifacts = artifact.Dependencies.OfType<AssetManifestArtifact>().ToList();

        if (assetArtifacts.Any(a => a.Contents.DockerImages?.Count > 0))
        {
            logger.LogError("Container image assets are currently not supported");
            throw new AWSProvisioningException("Failed to provision stack assets. Provisioning container image assets are currently not supported.");
        }

        var uploader = new CDKAssetUploader(resource.AWSSDKConfig, logger);
        foreach (var assetArtifact in assetArtifacts)
        {
            await uploader.UploadAssetsAsync(assetArtifact, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override void HandleTemplateProvisioningException(Exception ex, StackResource resource, ILogger logger)
    {
        if (ex.InnerException is AmazonCloudFormationException inner && inner.Message.StartsWith(@"Unable to fetch parameters [/cdk-bootstrap/"))
        {
            logger.LogError("The environment doesn't have the CDK toolkit stack installed. Use 'cdk boostrap' to setup your environment for use AWS CDK with Aspire");
        }
    }
}
