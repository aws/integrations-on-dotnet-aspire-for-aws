// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Constructs;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Environments.CDKDefaults;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public interface IAWSPublishTarget
{
    string PublishTargetName { get; }

    Type PublishTargetAnnotation { get; }

    Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation publishAnnotation, CancellationToken cancellationToken);

    IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct);

    IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource);
}

public class IsDefaultPublishTargetMatchResult
{
    public const int DEFAULT_MATCH_RANK = 100;

    public static readonly IsDefaultPublishTargetMatchResult NO_MATCH = new IsDefaultPublishTargetMatchResult { IsMatch = false };

    public bool IsMatch { get; set; }

    public IResourceAnnotation? PublishTargetAnnotation { get; set; }

    public int Rank { get; set; } = DEFAULT_MATCH_RANK;
}
