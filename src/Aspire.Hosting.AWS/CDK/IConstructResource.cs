// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.CDK;

/// <summary>
/// Resource representing an AWS CDK construct.
/// </summary>
public interface IConstructResource : IResourceWithParent<IResourceWithConstruct>, IResourceWithConstruct, IResourceWithWaitSupport;
