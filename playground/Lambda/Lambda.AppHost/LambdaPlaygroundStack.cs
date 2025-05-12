// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace Lambda.AppHost;

public class LambdaPlaygroundStack : Stack
{
    public LambdaPlaygroundStack(Construct scope, string id, IStackProps? props = null) 
        : base(scope, id, props)
    {
        Aspects.Of(this).Add(new Tag("aws-tests", "AWSLambdaPlaygroundResources"));
        Aspects.Of(this).Add(new Tag("aws-repo", "integrations-on-dotnet-aspire-for-aws"));
    }
}
