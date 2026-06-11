#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.AgentCore;

/// <summary>
/// Represents an AgentCore Runtime resource.
/// Wraps a .NET project that implements the AgentCore service contract (POST /invocations, GET /ping).
/// </summary>
[Experimental(Constants.ASPIREAWSAGENTCORE001)]
public class AgentCoreRuntimeResource : ProjectResource
{
    /// <summary>
    /// Creates an instance of <see cref="AgentCoreRuntimeResource"/>.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    public AgentCoreRuntimeResource(string name) : base(name)
    {
    }
}
#endif
