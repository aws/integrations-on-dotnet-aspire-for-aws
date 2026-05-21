// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace AWSCDK.AppHost;

public class CustomStack : Stack
{
    public IBucket Bucket { get; }

    public IQueue Queue { get; }

    public CustomStack(Construct scope, string id)
        : base(scope, id)
    {
        Bucket = new Bucket(this, "Bucket");
        Queue = new Queue(this, "Queue");

        // BucketDeployment is included to demonstrate Aspire's CDK file asset provisioning.
        // It creates a Lambda-backed custom resource whose handler is packaged as a file asset,
        // which Aspire uploads to the CDK bootstrap bucket before deploying the stack.
        new BucketDeployment(this, "SampleContentDeployment", new BucketDeploymentProps
        {
            Sources = [Source.Asset("./sample-content")],
            DestinationBucket = Bucket,
        });
    }
}
