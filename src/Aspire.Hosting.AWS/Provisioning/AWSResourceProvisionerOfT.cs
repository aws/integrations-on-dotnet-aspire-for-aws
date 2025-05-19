// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Provisioning;

internal interface IAWSResourceProvisioner
{
    Task GetOrCreateResourceAsync(IAWSResource resource, CancellationToken cancellationToken = default);
}

internal abstract class AWSResourceProvisioner<TResource>(ResourceNotificationService notificationService) : IAWSResourceProvisioner
    where TResource : IAWSResource
{
    protected ResourceNotificationService NotificationService => notificationService;

    public async Task GetOrCreateResourceAsync(
        IAWSResource resource,
        CancellationToken cancellationToken)
    {
        if (resource is IResourceWithWaitSupport waitResource)
        {
            await notificationService.WaitForDependenciesAsync(waitResource, cancellationToken).ConfigureAwait(false);
            await NotificationService.PublishUpdateAsync(resource, state => state with
            {
                State = new ResourceStateSnapshot("Starting", KnownResourceStateStyles.Info)
            }).ConfigureAwait(false);
        }

        await GetOrCreateResourceAsync((TResource)resource, cancellationToken);
    }

    protected abstract Task GetOrCreateResourceAsync(TResource resource, CancellationToken cancellationToken);
}
