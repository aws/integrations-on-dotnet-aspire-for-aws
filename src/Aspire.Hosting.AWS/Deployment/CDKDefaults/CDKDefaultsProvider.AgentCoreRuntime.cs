// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#if NET10_0_OR_GREATER

using Amazon.CDK;
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
    private bool _defaultAgentCoreRuntimeRoleCreatedByIntegration;

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
                _defaultAgentCoreRuntimeRoleCreatedByIntegration = false;
            }
            else
            {
                _defaultAgentCoreRuntimeRole = CreateDefaultAgentCoreRuntimeRole();
                _defaultAgentCoreRuntimeRoleCreatedByIntegration = true;
            }
        }

        return _defaultAgentCoreRuntimeRole;
    }

    /// <summary>
    /// Creates the default IAM role for AgentCore runtimes with a trust policy for the Bedrock AgentCore service principal.
    /// </summary>
    /// <returns>The created IAM role.</returns>
    protected virtual IRole CreateDefaultAgentCoreRuntimeRole()
    {
        var stack = EnvironmentResource.CDKStack;

        // Bedrock AgentCore runtimes are assumed by the bedrock-agentcore service principal (NOT
        // bedrock.amazonaws.com). The trust policy is scoped with aws:SourceAccount/aws:SourceArn
        // conditions to guard against the confused-deputy problem, per the AgentCore documentation.
        var role = new Role(stack, "DefaultAgentCoreRuntimeRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("bedrock-agentcore.amazonaws.com", new ServicePrincipalOpts
            {
                Conditions = new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, object>
                    {
                        ["aws:SourceAccount"] = stack.Account
                    },
                    ["ArnLike"] = new Dictionary<string, object>
                    {
                        ["aws:SourceArn"] = $"arn:{stack.Partition}:bedrock-agentcore:{stack.Region}:{stack.Account}:*"
                    }
                }
            }),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("AmazonEC2ContainerRegistryReadOnly"),
                ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"),
            }
        });

        // The agent code running in the runtime invokes Bedrock models, so grant the model invocation
        // actions (InvokeModel, InvokeModelWithResponseStream, etc.) via the InvokeModel* wildcard.
        // Invocation can target a model directly or route through a (cross-region) inference profile, so
        // the policy covers both: foundation models (account-less ARN, region wildcarded so a global
        // profile's underlying models in any region are allowed) and inference profiles in this account.
        role.AddToPrincipalPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "bedrock:InvokeModel*" },
            Resources = new[]
            {
                $"arn:{stack.Partition}:bedrock:*::foundation-model/*",
                $"arn:{stack.Partition}:bedrock:*:{stack.Account}:inference-profile/*"
            }
        }));

        return role;
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

    /// <summary>
    /// Gets the default event expiry duration, in days, applied to an AgentCore memory when the user has
    /// not specified one.
    /// </summary>
    public virtual double AgentCoreMemoryEventExpiryDurationDays => 90;

    /// <summary>
    /// Applies default configuration values to the specified AgentCore memory properties if they are not already set.
    /// </summary>
    /// <param name="props">The AgentCore memory properties to which default values will be applied.</param>
    protected internal virtual void ApplyAgentCoreMemoryDefaults(CfnMemoryProps props)
    {
        // A short-term (event) memory only requires a name and an event expiry duration. The name is set
        // by the publish target (it knows the stack and resource name), so only default the expiry here.
        // Long-term memory strategies and a memory execution role are left to the user via the memory
        // props callback.
        // EventExpiryDuration is a non-nullable double on CfnMemoryProps; treat the default 0 as unset.
        if (props.EventExpiryDuration == 0)
        {
            props.EventExpiryDuration = AgentCoreMemoryEventExpiryDurationDays;
        }
    }

    /// <summary>
    /// Grants the default AgentCore runtime role permission to use the data-plane APIs of the AgentCore
    /// memory identified by <paramref name="memoryArn"/>. The agent code running in the runtime reads and
    /// writes memory events and records, so its role needs these permissions, scoped to the memory and its
    /// child resources.
    /// <para>
    /// When the runtime role was supplied by the user (rather than created by the integration) the role is
    /// left untouched, mirroring how user-supplied roles are never mutated elsewhere.
    /// </para>
    /// </summary>
    /// <param name="memoryArn">The ARN of the AgentCore memory to grant access to.</param>
    internal void GrantAgentCoreRuntimeMemoryAccess(string memoryArn)
    {
        var role = GetDefaultAgentCoreRuntimeRole();
        if (!_defaultAgentCoreRuntimeRoleCreatedByIntegration)
        {
            return;
        }

        role.AddToPrincipalPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "bedrock-agentcore:CreateEvent",
                "bedrock-agentcore:GetEvent",
                "bedrock-agentcore:ListEvents",
                "bedrock-agentcore:DeleteEvent",
                "bedrock-agentcore:ListActors",
                "bedrock-agentcore:ListSessions",
                "bedrock-agentcore:RetrieveMemoryRecords",
                "bedrock-agentcore:GetMemoryRecord",
                "bedrock-agentcore:ListMemoryRecords"
            },
            Resources = new[]
            {
                memoryArn,
                Fn.Join("", new[] { memoryArn, "/*" })
            }
        }));
    }
}

#endif
