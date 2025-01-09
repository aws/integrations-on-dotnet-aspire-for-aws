// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.S3;
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
    }

}
