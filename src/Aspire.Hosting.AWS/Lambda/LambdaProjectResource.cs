// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Aspire resource representing a Lambda function.
/// </summary>
/// <param name="name"></param>
public class LambdaProjectResource : ProjectResource
{
    public LambdaProjectResource(string name)
        : base(name)
    {
#pragma warning disable ASPIREPIPELINES001
        // Remove the default PipelineStepAnnotation added by ProjectResource which will trigger a container build not compatible
        // with the Lambda project.
        var addedPipelineAnnotations = Annotations.Where(a => a.GetType() == typeof(PipelineStepAnnotation)).ToList();
        foreach (var annotation in addedPipelineAnnotations)
        {
            Annotations.Remove(annotation);
        }
#pragma warning restore ASPIREPIPELINES001

    }
}
