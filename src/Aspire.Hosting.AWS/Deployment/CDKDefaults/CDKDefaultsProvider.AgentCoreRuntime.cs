// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#if NET10_0_OR_GREATER

using Amazon.CDK.AWS.BedrockAgentCore;
using Amazon.CDK.AWS.IAM;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the default network mode for AgentCore runtimes.
    /// </summary>
    /// <remarks>
    /// Default is "PUBLIC" which means the runtime is accessible without requiring a VPC.
    /// </remarks>
    public virtual string AgentCoreRuntimeNetworkMode => "PUBLIC";

    private IRole? _defaultAgentCoreRuntimeRole;

    /// <summary>
    /// Gets the default IAM role for AgentCore runtimes. The role is shared across all AgentCore runtimes in the stack.
    /// </summary>
    /// <returns>The default IAM role for AgentCore runtimes.</returns>
    public IRole GetDefaultAgentCoreRuntimeRole()
    {
        if (_defaultAgentCoreRuntimeRole == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultAgentCoreRuntimeRoleAttribute, IRole>();
            if (definedDefault != null)
            {
                _defaultAgentCoreRuntimeRole = definedDefault;
            }
            else
            {
                _defaultAgentCoreRuntimeRole = CreateDefaultAgentCoreRuntimeRole();
            }
        }

        return _defaultAgentCoreRuntimeRole;
    }

    /// <summary>
    /// Creates the default IAM role for AgentCore runtimes with trust policy for the bedrock service principal.
    /// </summary>
    /// <returns>The created IAM role.</returns>
    protected virtual IRole CreateDefaultAgentCoreRuntimeRole()
    {
        return new Role(EnvironmentResource.CDKStack, "DefaultAgentCoreRuntimeRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("bedrock.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("AmazonEC2ContainerRegistryReadOnly"),
                ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"),
            }
        });
    }

    /// <summary>
    /// Applies default configuration values to the specified AgentCore runtime properties if they are not already set.
    /// </summary>
    /// <param name="props">The AgentCore runtime properties to which default values will be applied.</param>
    protected internal virtual void ApplyAgentCoreRuntimeDefaults(CfnRuntimeProps props)
    {
        if (props.NetworkConfiguration == null)
        {
            props.NetworkConfiguration = new CfnRuntime.NetworkConfigurationProperty
            {
                NetworkMode = AgentCoreRuntimeNetworkMode
            };
        }

        if (string.IsNullOrEmpty(props.RoleArn))
        {
            props.RoleArn = GetDefaultAgentCoreRuntimeRole().RoleArn;
        }
    }
}

#endif
