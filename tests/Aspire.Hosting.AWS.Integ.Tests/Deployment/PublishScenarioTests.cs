using Aspire.Hosting.AWS.Deployment.Services;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Publishing;
using DeploymentTestApp.AppHost;
using System.Text.Json;

using static Aspire.Hosting.AWS.Integ.Tests.Deployment.CloudFormationJsonUtilities;

namespace Aspire.Hosting.AWS.Integ.Tests.Deployment;

#pragma warning disable ASPIREPIPELINES003

[Collection("CDKDeploymentTests")]
public class PublishScenarioTests
{
    [Fact]
    public async Task TestPublishWebApp2ReferenceOnWebApp1()
    {
        var cloudFormationValidation = (JsonDocument cfTemplateDoc) =>
        {
            AssertElementExistsAtPath(cfTemplateDoc, "Outputs/WebApp1ExpressGatewayEndpoint");
            AssertElementExistsAtPath(cfTemplateDoc, "Outputs/WebApp2ExpressGatewayEndpoint");

            var webApp1OutputFnJoin = AssertElementExistsAtPath(cfTemplateDoc, "Outputs/WebApp1ExpressGatewayEndpoint/Value/Fn::Join");
            AssertJsonEquals("""
            [
             "",
             [
              "https://",
              {
               "Fn::GetAtt": [
                "ProjectWebApp1",
                "Endpoint"
               ]
              },
              "/"
             ]
            ]
            """, webApp1OutputFnJoin);

            var webApp2EnvFnJoin = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp2/Properties/PrimaryContainer/Environment/{Name=services__WebApp1__https__0}/Value/Fn::Join");
            AssertJsonEquals("""
            [
             "",
             [
              "https://",
              {
               "Fn::GetAtt": [
                "ProjectWebApp1",
                "Endpoint"
               ]
              },
              "/"
             ]
            ]
            """, webApp2EnvFnJoin);

            var webApp1Memory = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/Memory");
            Assert.Equal("4096", webApp1Memory.GetString());

            var webApp2Memory = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp2/Properties/Memory");
            Assert.Equal("2048", webApp2Memory.GetString());

            var vpcs = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::VPC");
            Assert.Single(vpcs);

            var iamRoles = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Role");
            Assert.Equal(2, iamRoles.Count);

            var executionRole = iamRoles.FirstOrDefault(r => r.LogicalId.Contains("ExecutionRole"));            
            AssertJsonEquals("""
            {
             "Type": "AWS::IAM::Role",
             "Properties": {
              "AssumeRolePolicyDocument": {
               "Statement": [
                {
                 "Action": "sts:AssumeRole",
                 "Effect": "Allow",
                 "Principal": {
                  "Service": "ecs-tasks.amazonaws.com"
                 }
                }
               ],
               "Version": "2012-10-17"
              },
              "ManagedPolicyArns": [
               {
                "Fn::Join": [
                 "",
                 [
                  "arn:",
                  {
                   "Ref": "AWS::Partition"
                  },
                  ":iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
                 ]
                ]
               },
               {
                "Fn::Join": [
                 "",
                 [
                  "arn:",
                  {
                   "Ref": "AWS::Partition"
                  },
                  ":iam::aws:policy/CloudWatchLogsFullAccess"
                 ]
                ]
               }
              ]
             }
            }
            """, executionRole.Resource);

            var executionRoleAssignment = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/ExecutionRoleArn");
            AssertJsonEquals("""
            {
             "Fn::GetAtt": [
              "PLACE_HOLDER",
              "Arn"
             ]
            }
            """.Replace("PLACE_HOLDER", executionRole.LogicalId), executionRoleAssignment);

            var infrastructureRole = iamRoles.FirstOrDefault(r => r.LogicalId.Contains("InfrastructureRole"));
            AssertJsonEquals("""
            {
             "Type": "AWS::IAM::Role",
             "Properties": {
              "AssumeRolePolicyDocument": {
               "Statement": [
                {
                 "Action": "sts:AssumeRole",
                 "Effect": "Allow",
                 "Principal": {
                  "Service": "ecs.amazonaws.com"
                 }
                }
               ],
               "Version": "2012-10-17"
              },
              "ManagedPolicyArns": [
               {
                "Fn::Join": [
                 "",
                 [
                  "arn:",
                  {
                   "Ref": "AWS::Partition"
                  },
                  ":iam::aws:policy/service-role/AmazonECSInfrastructureRoleforExpressGatewayServices"
                 ]
                ]
               }
              ]
             }
            }
            """, infrastructureRole.Resource);

            var infrastructureRoleAssignment = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/InfrastructureRoleArn");
            AssertJsonEquals("""
            {
             "Fn::GetAtt": [
              "PLACE_HOLDER",
              "Arn"
             ]
            }
            """.Replace("PLACE_HOLDER", infrastructureRole.LogicalId), infrastructureRoleAssignment);

            var securityGroups = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroup");
            Assert.Single(securityGroups);

            var securityGroupAssignment = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/NetworkConfiguration/SecurityGroups");
            AssertJsonEquals("""
            [
             {
              "Fn::GetAtt": [
               "PLACE_HOLDER",
               "GroupId"
              ]
             }
            ]
            """.Replace("PLACE_HOLDER", securityGroups[0].LogicalId), securityGroupAssignment);

            var subnetAssignment = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/NetworkConfiguration/Subnets");
            Assert.Equal(2, subnetAssignment.GetArrayLength());

            var imageAssignment = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/PrimaryContainer/Image");
            Assert.True(imageAssignment.TryGetProperty("Fn::Sub", out var _));

            return Task.CompletedTask;
        };

        await ExecutePublishAsync(nameof(Scenarios.PublishWebApp2ReferenceOnWebApp1), cloudFormationValidation);
    }

    [Fact]
    public async Task TestPublishWebApp2ReferenceOnWebApp1WithAlb()
    {
        var cloudFormationValidation = (JsonDocument cfTemplateDoc) =>
        {
            // Validate outputs exist (4 outputs: LoadBalancer DNS and Service URL for each app)
            AssertElementExistsAtPath(cfTemplateDoc, "Outputs/ProjectWebApp1LoadBalancerDNS6FD08CD4");
            AssertElementExistsAtPath(cfTemplateDoc, "Outputs/ProjectWebApp1ServiceURLA85DFCF2");
            AssertElementExistsAtPath(cfTemplateDoc, "Outputs/ProjectWebApp2LoadBalancerDNS3CB6913E");
            AssertElementExistsAtPath(cfTemplateDoc, "Outputs/ProjectWebApp2ServiceURL01CD6E09");

            // Validate WebApp1 Service URL output uses Fn::Join with load balancer DNS
            var webApp1ServiceUrlFnJoin = AssertElementExistsAtPath(cfTemplateDoc, "Outputs/ProjectWebApp1ServiceURLA85DFCF2/Value/Fn::Join");
            Assert.True(webApp1ServiceUrlFnJoin.GetArrayLength() >= 2);

            // Validate VPC count
            var vpcs = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::VPC");
            Assert.Single(vpcs);

            // Validate ECS Cluster exists
            var ecsClusters = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::Cluster");
            Assert.Single(ecsClusters);

            // Validate Application Load Balancers (2 total: one for each app)
            var loadBalancers = GetResourcesOfType(cfTemplateDoc, "AWS::ElasticLoadBalancingV2::LoadBalancer");
            Assert.Equal(2, loadBalancers.Count);

            var webApp1LB = loadBalancers.FirstOrDefault(lb => lb.LogicalId.Contains("WebApp1"));
            Assert.NotNull(webApp1LB.LogicalId);
            var webApp1LBType = AssertElementExistsAtPath(webApp1LB.Resource, "Properties/Type");
            Assert.Equal("application", webApp1LBType.GetString());
            var webApp1LBScheme = AssertElementExistsAtPath(webApp1LB.Resource, "Properties/Scheme");
            Assert.Equal("internet-facing", webApp1LBScheme.GetString());

            var webApp2LB = loadBalancers.FirstOrDefault(lb => lb.LogicalId.Contains("WebApp2"));
            Assert.NotNull(webApp2LB.LogicalId);
            var webApp2LBType = AssertElementExistsAtPath(webApp2LB.Resource, "Properties/Type");
            Assert.Equal("application", webApp2LBType.GetString());

            // Validate Target Groups (2 total)
            var targetGroups = GetResourcesOfType(cfTemplateDoc, "AWS::ElasticLoadBalancingV2::TargetGroup");
            Assert.Equal(2, targetGroups.Count);

            // Validate Listeners (2 total)
            var listeners = GetResourcesOfType(cfTemplateDoc, "AWS::ElasticLoadBalancingV2::Listener");
            Assert.Equal(2, listeners.Count);

            // Validate listener port is 80
            var webApp1Listener = listeners.FirstOrDefault(l => l.LogicalId.Contains("WebApp1"));
            Assert.NotNull(webApp1Listener.LogicalId);
            var webApp1ListenerPort = AssertElementExistsAtPath(webApp1Listener.Resource, "Properties/Port");
            Assert.Equal(80, webApp1ListenerPort.GetInt32());
            var webApp1ListenerProtocol = AssertElementExistsAtPath(webApp1Listener.Resource, "Properties/Protocol");
            Assert.Equal("HTTP", webApp1ListenerProtocol.GetString());

            // Validate Security Groups (3 total: 1 for ECS cluster, 2 for load balancers)
            var securityGroups = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroup");
            Assert.Equal(3, securityGroups.Count);

            // Validate ECS TaskDefinitions (2 total)
            var taskDefinitions = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::TaskDefinition");
            Assert.Equal(2, taskDefinitions.Count);

            var webApp1TaskDef = taskDefinitions.FirstOrDefault(td => td.LogicalId.Contains("WebApp1"));
            Assert.NotNull(webApp1TaskDef.LogicalId);

            // Validate WebApp1 TaskDefinition properties
            var webApp1Cpu = AssertElementExistsAtPath(webApp1TaskDef.Resource, "Properties/Cpu");
            Assert.Equal("1024", webApp1Cpu.GetString());
            var webApp1Memory = AssertElementExistsAtPath(webApp1TaskDef.Resource, "Properties/Memory");
            Assert.Equal("4096", webApp1Memory.GetString());

            // Validate WebApp1 container port
            var webApp1ContainerPort = AssertElementExistsAtPath(webApp1TaskDef.Resource, "Properties/ContainerDefinitions/{Name=web}/PortMappings/0/ContainerPort");
            Assert.Equal(8080, webApp1ContainerPort.GetInt32());

            var webApp2TaskDef = taskDefinitions.FirstOrDefault(td => td.LogicalId.Contains("WebApp2"));
            Assert.NotNull(webApp2TaskDef.LogicalId);

            // Validate WebApp2 container references WebApp1's load balancer DNS
            var webApp2EnvFnJoin = AssertElementExistsAtPath(cfTemplateDoc, $"Resources/{webApp2TaskDef.LogicalId}/Properties/ContainerDefinitions/{{Name=web}}/Environment/{{Name=services__WebApp1__http__0}}/Value/Fn::Join");
            AssertJsonEquals($$"""
            [
             "",
             [
              "http://",
              {
               "Fn::GetAtt": [
                "{{webApp1LB.LogicalId}}",
                "DNSName"
               ]
              },
              ":80/"
             ]
            ]
            """, webApp2EnvFnJoin);

            // Validate WebApp2 container has logging configured
            var webApp2LogConfig = AssertElementExistsAtPath(webApp2TaskDef.Resource, "Properties/ContainerDefinitions/{Name=web}/LogConfiguration");
            var logDriver = AssertElementExistsAtPath(webApp2LogConfig, "LogDriver");
            Assert.Equal("awslogs", logDriver.GetString());

            // Validate ECS Services (2 total)
            var ecsServices = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::Service");
            Assert.Equal(2, ecsServices.Count);

            var webApp1Service = ecsServices.FirstOrDefault(s => s.LogicalId.Contains("WebApp1"));
            Assert.NotNull(webApp1Service.LogicalId);

            // Validate WebApp1 Service is Fargate
            var webApp1LaunchType = AssertElementExistsAtPath(webApp1Service.Resource, "Properties/LaunchType");
            Assert.Equal("FARGATE", webApp1LaunchType.GetString());

            // Validate WebApp1 Service uses private subnets
            var webApp1AssignPublicIp = AssertElementExistsAtPath(webApp1Service.Resource, "Properties/NetworkConfiguration/AwsvpcConfiguration/AssignPublicIp");
            Assert.Equal("DISABLED", webApp1AssignPublicIp.GetString());

            // Validate WebApp1 Service desired count
            var webApp1DesiredCount = AssertElementExistsAtPath(webApp1Service.Resource, "Properties/DesiredCount");
            Assert.Equal(2, webApp1DesiredCount.GetInt32());

            // Validate WebApp1 Service deployment configuration
            var webApp1MinHealthyPercent = AssertElementExistsAtPath(webApp1Service.Resource, "Properties/DeploymentConfiguration/MinimumHealthyPercent");
            Assert.Equal(100, webApp1MinHealthyPercent.GetInt32());

            // Validate WebApp1 Service has load balancer configured
            var webApp1LoadBalancers = AssertElementExistsAtPath(webApp1Service.Resource, "Properties/LoadBalancers");
            Assert.Equal(1, webApp1LoadBalancers.GetArrayLength());
            var webApp1LBContainerPort = AssertElementExistsAtPath(webApp1LoadBalancers, "0/ContainerPort");
            Assert.Equal(8080, webApp1LBContainerPort.GetInt32());

            // Validate WebApp1 Service has deployment tag
            var webApp1Tags = AssertElementExistsAtPath(webApp1Service.Resource, "Properties/Tags");
            var webApp1DeploymentTag = webApp1Tags.EnumerateArray()
                .FirstOrDefault(t => t.GetProperty("Key").GetString() == "aspire:deployment-tag");
            Assert.NotEqual(default, webApp1DeploymentTag);

            var webApp2Service = ecsServices.FirstOrDefault(s => s.LogicalId.Contains("WebApp2"));
            Assert.NotNull(webApp2Service.LogicalId);

            // Validate WebApp2 Service desired count
            var webApp2DesiredCount = AssertElementExistsAtPath(webApp2Service.Resource, "Properties/DesiredCount");
            Assert.Equal(2, webApp2DesiredCount.GetInt32());

            // Validate WebApp2 Service depends on WebApp1 resources
            var webApp2ServiceDependsOn = AssertElementExistsAtPath(webApp2Service.Resource, "DependsOn");
            Assert.Equal(JsonValueKind.Array, webApp2ServiceDependsOn.ValueKind);
            var dependsOnArray = webApp2ServiceDependsOn.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains(webApp1Service.LogicalId, dependsOnArray);

            // Validate Log Groups (2 total)
            var logGroups = GetResourcesOfType(cfTemplateDoc, "AWS::Logs::LogGroup");
            Assert.Equal(2, logGroups.Count);

            // Validate IAM Roles (4 total: TaskRole and ExecutionRole for each app)
            var iamRoles = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Role");
            Assert.Equal(4, iamRoles.Count);

            // Validate IAM Policies (2 total: ExecutionRole default policy for each app)
            var iamPolicies = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Policy");
            Assert.Equal(2, iamPolicies.Count);

            // Validate Security Group Ingress rules exist for load balancer to target communication
            var securityGroupIngress = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroupIngress");
            Assert.Equal(2, securityGroupIngress.Count);

            // Validate Security Group Egress rules exist
            var securityGroupEgress = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroupEgress");
            Assert.Equal(2, securityGroupEgress.Count);

            return Task.CompletedTask;
        };

        await ExecutePublishAsync(nameof(Scenarios.PublishWebApp2ReferenceOnWebApp1WithAlb), cloudFormationValidation);
    }

    [Fact]
    public async Task TestPublishService1ReferenceOnWebApp1()
    {
        var cloudFormationValidation = (JsonDocument cfTemplateDoc) =>
        {
            // Validate output exists (only WebApp1, no Service1 output since it's not a web app)
            AssertElementExistsAtPath(cfTemplateDoc, "Outputs/WebApp1ExpressGatewayEndpoint");

            // Validate WebApp1 output value
            var webApp1OutputFnJoin = AssertElementExistsAtPath(cfTemplateDoc, "Outputs/WebApp1ExpressGatewayEndpoint/Value/Fn::Join");
            AssertJsonEquals("""
            [
             "",
             [
              "https://",
              {
               "Fn::GetAtt": [
                "ProjectWebApp1",
                "Endpoint"
               ]
              },
              "/"
             ]
            ]
            """, webApp1OutputFnJoin);

            // Validate VPC count
            var vpcs = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::VPC");
            Assert.Single(vpcs);

            // Validate ECS Cluster exists
            var ecsClusters = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::Cluster");
            Assert.Single(ecsClusters);

            // Validate IAM Roles (4 total: 2 for Express, 2 for Service1)
            var iamRoles = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Role");
            Assert.Equal(4, iamRoles.Count);

            // Validate Security Groups
            var securityGroups = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroup");
            Assert.Single(securityGroups);

            // Validate ECS TaskDefinition for Service1
            var taskDefinitions = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::TaskDefinition");
            Assert.Single(taskDefinitions);
            var service1TaskDef = taskDefinitions[0];

            // Validate Service1 TaskDefinition properties
            var service1Cpu = AssertElementExistsAtPath(service1TaskDef.Resource, "Properties/Cpu");
            Assert.Equal("256", service1Cpu.GetString());

            var service1Memory = AssertElementExistsAtPath(service1TaskDef.Resource, "Properties/Memory");
            Assert.Equal("512", service1Memory.GetString());

            var service1NetworkMode = AssertElementExistsAtPath(service1TaskDef.Resource, "Properties/NetworkMode");
            Assert.Equal("awsvpc", service1NetworkMode.GetString());

            // Validate Service1 container references WebApp1
            var service1EnvFnJoin = AssertElementExistsAtPath(cfTemplateDoc, $"Resources/{service1TaskDef.LogicalId}/Properties/ContainerDefinitions/{{Name=Container-Service1}}/Environment/{{Name=services__WebApp1__https__0}}/Value/Fn::Join");
            AssertJsonEquals("""
            [
             "",
             [
              "https://",
              {
               "Fn::GetAtt": [
                "ProjectWebApp1",
                "Endpoint"
               ]
              },
              "/"
             ]
            ]
            """, service1EnvFnJoin);

            // Validate Service1 container has logging configured
            var service1LogConfig = AssertElementExistsAtPath(cfTemplateDoc, $"Resources/{service1TaskDef.LogicalId}/Properties/ContainerDefinitions/{{Name=Container-Service1}}/LogConfiguration");
            var logDriver = AssertElementExistsAtPath(service1LogConfig, "LogDriver");
            Assert.Equal("awslogs", logDriver.GetString());

            // Validate ECS Service for Service1
            var ecsServices = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::Service");
            Assert.Single(ecsServices);
            var service1 = ecsServices[0];

            // Validate Service1 is Fargate
            var launchType = AssertElementExistsAtPath(service1.Resource, "Properties/LaunchType");
            Assert.Equal("FARGATE", launchType.GetString());

            // Validate Service1 uses private subnets
            var service1Subnets = AssertElementExistsAtPath(service1.Resource, "Properties/NetworkConfiguration/AwsvpcConfiguration/Subnets");
            Assert.Equal(2, service1Subnets.GetArrayLength());

            // Validate Service1 has AssignPublicIp disabled (private subnet)
            var assignPublicIp = AssertElementExistsAtPath(service1.Resource, "Properties/NetworkConfiguration/AwsvpcConfiguration/AssignPublicIp");
            Assert.Equal("DISABLED", assignPublicIp.GetString());

            // Validate Service1 deployment configuration
            var desiredCount = AssertElementExistsAtPath(service1.Resource, "Properties/DesiredCount");
            Assert.Equal(1, desiredCount.GetInt32());

            var minHealthyPercent = AssertElementExistsAtPath(service1.Resource, "Properties/DeploymentConfiguration/MinimumHealthyPercent");
            Assert.Equal(100, minHealthyPercent.GetInt32());

            // Validate Service1 depends on WebApp1
            var service1DependsOn = AssertElementExistsAtPath(service1.Resource, "DependsOn");
            Assert.Equal(JsonValueKind.Array, service1DependsOn.ValueKind);
            var dependsOnArray = service1DependsOn.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains("ProjectWebApp1", dependsOnArray);

            // Validate Service1 has deployment tag
            var service1Tags = AssertElementExistsAtPath(service1.Resource, "Properties/Tags");
            var deploymentTag = service1Tags.EnumerateArray()
                .FirstOrDefault(t => t.GetProperty("Key").GetString() == "aspire:deployment-tag");
            Assert.NotEqual(default, deploymentTag);

            // Validate Log Group exists
            var logGroups = GetResourcesOfType(cfTemplateDoc, "AWS::Logs::LogGroup");
            Assert.Single(logGroups);

            // Validate IAM Policy for Service1 Execution Role
            var iamPolicies = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Policy");
            Assert.Single(iamPolicies);

            return Task.CompletedTask;
        };

        await ExecutePublishAsync(nameof(Scenarios.PublishService1ReferenceOnWebApp1), cloudFormationValidation);
    }

    [Fact]
    public async Task TestPublishWebApp1UsingDefaultVpc()
    {
        var cloudFormationValidation = (JsonDocument cfTemplateDoc) =>
        {
            // Validate NO VPC resource is created (using default VPC)
            var vpcs = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::VPC");
            Assert.Empty(vpcs);

            // Validate NO Subnet resources are created (using default VPC subnets)
            var subnets = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::Subnet");
            Assert.Empty(subnets);

            // Validate NO Route Tables are created
            var routeTables = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::RouteTable");
            Assert.Empty(routeTables);

            // Validate NO NAT Gateways are created
            var natGateways = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::NatGateway");
            Assert.Empty(natGateways);

            // Validate NO Internet Gateways are created
            var internetGateways = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::InternetGateway");
            Assert.Empty(internetGateways);

            // Validate Security Group exists
            var securityGroups = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroup");
            Assert.Single(securityGroups);

            // Validate Security Group references default VPC directly (string, not Ref)
            var securityGroupVpcId = AssertElementExistsAtPath(securityGroups[0].Resource, "Properties/VpcId");
            Assert.Equal(JsonValueKind.String, securityGroupVpcId.ValueKind);
            Assert.StartsWith("vpc-", securityGroupVpcId.GetString());

            // Validate ExpressGatewayService exists
            var expressGatewayServices = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::ExpressGatewayService");
            Assert.Single(expressGatewayServices);

            // Validate WebApp1 configuration
            AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1");

            // Validate WebApp1 network configuration references subnets
            var subnetAssignment = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/NetworkConfiguration/Subnets");
            Assert.Equal(JsonValueKind.Array, subnetAssignment.ValueKind);
            Assert.True(subnetAssignment.GetArrayLength() > 0, "WebApp1 should reference at least one subnet");

            // Validate subnets are direct string IDs (from default VPC lookup), not Refs
            foreach (var subnet in subnetAssignment.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.String, subnet.ValueKind);
                Assert.StartsWith("subnet-", subnet.GetString());
            }

            // Validate security group assignment
            var securityGroupAssignment = AssertElementExistsAtPath(cfTemplateDoc, "Resources/ProjectWebApp1/Properties/NetworkConfiguration/SecurityGroups");
            Assert.Equal(1, securityGroupAssignment.GetArrayLength());
            AssertJsonEquals($$"""
            [
             {
              "Fn::GetAtt": [
               "{{securityGroups[0].LogicalId}}",
               "GroupId"
              ]
             }
            ]
            """, securityGroupAssignment);

            return Task.CompletedTask;
        };
        await ExecutePublishAsync(nameof(Scenarios.PublishWebApp1UsingDefaultVpc), cloudFormationValidation);
    }

    [Fact]
    public async Task TestPublishLambda()
    {
        var cloudFormationValidation = (JsonDocument cfTemplateDoc) =>
        {
            // Validate NO VPC resources are created
            var vpcs = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::VPC");
            Assert.Empty(vpcs);

            // Validate NO Subnet resources are created
            var subnets = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::Subnet");
            Assert.Empty(subnets);

            // Validate NO Security Groups are created
            var securityGroups = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroup");
            Assert.Empty(securityGroups);

            // Validate NO ECS resources are created
            var ecsClusters = GetResourcesOfType(cfTemplateDoc, "AWS::ECS::Cluster");
            Assert.Empty(ecsClusters);

            // Validate NO Outputs (Lambda doesn't expose an endpoint by default)
            var outputsElement = GetElementAtPath(cfTemplateDoc, "Outputs");
            Assert.Null(outputsElement);

            // Validate Lambda Function exists
            var lambdaFunctions = GetResourcesOfType(cfTemplateDoc, "AWS::Lambda::Function");
            Assert.Single(lambdaFunctions);
            var lambdaFunction = lambdaFunctions[0];

            // Validate Lambda has NO VpcConfig (not attached to VPC)
            var vpcConfig = GetElementAtPath(lambdaFunction.Resource, "Properties/VpcConfig");
            Assert.Null(vpcConfig);

            // Validate Lambda runtime
            var runtime = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Runtime");
            Assert.Equal("dotnet8", runtime.GetString());

            // Validate Lambda memory
            var memorySize = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/MemorySize");
            Assert.Equal(512, memorySize.GetInt32());

            // Validate Lambda timeout
            var timeout = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Timeout");
            Assert.Equal(30, timeout.GetInt32());

            // Validate Lambda handler
            var handler = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Handler");
            Assert.Contains("FunctionHandler", handler.GetString());

            // Validate Lambda has deployment tag
            var lambdaTags = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Tags");
            var deploymentTag = lambdaTags.EnumerateArray()
                .FirstOrDefault(t => t.GetProperty("Key").GetString() == "aspire:deployment-tag");
            Assert.NotEqual(default, deploymentTag);

            // Validate Lambda code is from S3
            var codeS3Bucket = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Code/S3Bucket");
            Assert.NotNull(codeS3Bucket.GetString());
            var codeS3Key = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Code/S3Key");
            Assert.EndsWith(".zip", codeS3Key.GetString());

            // Validate Lambda role assignment
            var roleAssignment = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Role");
            Assert.True(roleAssignment.TryGetProperty("Fn::GetAtt", out var _));

            // Validate IAM Role exists for Lambda
            var iamRoles = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Role");
            Assert.Single(iamRoles);
            var lambdaRole = iamRoles[0];

            // Validate IAM Role has Lambda service principal
            var assumeRolePrincipal = AssertElementExistsAtPath(lambdaRole.Resource, "Properties/AssumeRolePolicyDocument/Statement/0/Principal/Service");
            Assert.Equal("lambda.amazonaws.com", assumeRolePrincipal.GetString());

            // Validate IAM Role has basic Lambda execution policy
            var managedPolicyArns = AssertElementExistsAtPath(lambdaRole.Resource, "Properties/ManagedPolicyArns");
            Assert.True(managedPolicyArns.GetArrayLength() >= 1);

            // Validate managed policy contains AWSLambdaBasicExecutionRole
            var firstPolicyFnJoin = AssertElementExistsAtPath(managedPolicyArns, "0/Fn::Join");
            var policyParts = firstPolicyFnJoin.EnumerateArray().ToList();
            Assert.True(policyParts.Count >= 2);
            var policyPartsArray = policyParts[1].EnumerateArray().ToList();
            var lastPart = policyPartsArray.Last();
            Assert.Contains("AWSLambdaBasicExecutionRole", lastPart.GetString());

            // Validate IAM Role has deployment tag
            var roleTags = AssertElementExistsAtPath(lambdaRole.Resource, "Properties/Tags");
            var roleDeploymentTag = roleTags.EnumerateArray()
                .FirstOrDefault(t => t.GetProperty("Key").GetString() == "aspire:deployment-tag");
            Assert.NotEqual(default, roleDeploymentTag);

            // Validate Lambda depends on IAM Role
            var lambdaDependsOn = AssertElementExistsAtPath(lambdaFunction.Resource, "DependsOn");
            Assert.Equal(JsonValueKind.Array, lambdaDependsOn.ValueKind);
            var dependsOnArray = lambdaDependsOn.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains(lambdaRole.LogicalId, dependsOnArray);

            return Task.CompletedTask;
        };

        await ExecutePublishAsync(nameof(Scenarios.PublishLambda), cloudFormationValidation);
    }

    [Fact]
    public async Task TestPublishLambdaWithCustomization()
    {
        var cloudFormationValidation = (JsonDocument cfTemplateDoc) =>
        {
            // Validate NO VPC resources are created
            var vpcs = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::VPC");
            Assert.Empty(vpcs);

            var subnets = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::Subnet");
            Assert.Empty(subnets);

            var securityGroups = GetResourcesOfType(cfTemplateDoc, "AWS::EC2::SecurityGroup");
            Assert.Empty(securityGroups);

            // Validate SQS Queue exists (created by custom stack)
            var sqsQueues = GetResourcesOfType(cfTemplateDoc, "AWS::SQS::Queue");
            Assert.Single(sqsQueues);
            var sqsQueue = sqsQueues[0];

            // Validate Lambda Function exists
            var lambdaFunctions = GetResourcesOfType(cfTemplateDoc, "AWS::Lambda::Function");
            Assert.Single(lambdaFunctions);
            var lambdaFunction = lambdaFunctions[0];

            // Validate Lambda has NO VpcConfig (not attached to VPC)
            var vpcConfig = GetElementAtPath(lambdaFunction.Resource, "Properties/VpcConfig");
            Assert.Null(vpcConfig);

            // Validate Lambda runtime
            var runtime = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Runtime");
            Assert.Equal("dotnet8", runtime.GetString());

            // Validate Lambda CUSTOMIZED memory (2048 instead of default 512)
            var memorySize = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/MemorySize");
            Assert.Equal(2048, memorySize.GetInt32());

            // Validate Lambda CUSTOMIZED timeout (120 instead of default 30)
            var timeout = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Timeout");
            Assert.Equal(120, timeout.GetInt32());

            // Validate Lambda handler
            var handler = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Handler");
            Assert.Contains("FunctionHandler", handler.GetString());

            // Validate Lambda has deployment tag
            var lambdaTags = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Tags");
            var deploymentTag = lambdaTags.EnumerateArray()
                .FirstOrDefault(t => t.GetProperty("Key").GetString() == "aspire:deployment-tag");
            Assert.NotEqual(default, deploymentTag);

            // Validate Event Source Mapping exists (Lambda -> SQS)
            var eventSourceMappings = GetResourcesOfType(cfTemplateDoc, "AWS::Lambda::EventSourceMapping");
            Assert.Single(eventSourceMappings);
            var eventSourceMapping = eventSourceMappings[0];

            // Validate Event Source Mapping BatchSize
            var batchSize = AssertElementExistsAtPath(eventSourceMapping.Resource, "Properties/BatchSize");
            Assert.Equal(5, batchSize.GetInt32());

            // Validate Event Source Mapping is Enabled
            var enabled = AssertElementExistsAtPath(eventSourceMapping.Resource, "Properties/Enabled");
            Assert.True(enabled.GetBoolean());

            // Validate Event Source Mapping references the SQS Queue
            var eventSourceArn = AssertElementExistsAtPath(eventSourceMapping.Resource, "Properties/EventSourceArn/Fn::GetAtt");
            var eventSourceArnArray = eventSourceArn.EnumerateArray().ToList();
            Assert.Equal(2, eventSourceArnArray.Count);
            Assert.Equal(sqsQueue.LogicalId, eventSourceArnArray[0].GetString());
            Assert.Equal("Arn", eventSourceArnArray[1].GetString());

            // Validate Event Source Mapping references the Lambda Function
            var functionName = AssertElementExistsAtPath(eventSourceMapping.Resource, "Properties/FunctionName/Ref");
            Assert.Equal(lambdaFunction.LogicalId, functionName.GetString());

            // Validate Event Source Mapping has deployment tag
            var eventSourceTags = AssertElementExistsAtPath(eventSourceMapping.Resource, "Properties/Tags");
            var eventSourceDeploymentTag = eventSourceTags.EnumerateArray()
                .FirstOrDefault(t => t.GetProperty("Key").GetString() == "aspire:deployment-tag");
            Assert.NotEqual(default, eventSourceDeploymentTag);

            // Validate IAM Role exists for Lambda
            var iamRoles = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Role");
            Assert.Single(iamRoles);
            var lambdaRole = iamRoles[0];

            // Validate IAM Role has Lambda service principal
            var assumeRolePrincipal = AssertElementExistsAtPath(lambdaRole.Resource, "Properties/AssumeRolePolicyDocument/Statement/0/Principal/Service");
            Assert.Equal("lambda.amazonaws.com", assumeRolePrincipal.GetString());

            // Validate IAM Policy exists with SQS permissions
            var iamPolicies = GetResourcesOfType(cfTemplateDoc, "AWS::IAM::Policy");
            Assert.Single(iamPolicies);
            var sqsPolicy = iamPolicies[0];

            // Validate IAM Policy has SQS actions
            var policyStatements = AssertElementExistsAtPath(sqsPolicy.Resource, "Properties/PolicyDocument/Statement");
            Assert.True(policyStatements.GetArrayLength() >= 1);

            var sqsStatement = policyStatements.EnumerateArray().First();
            var actions = AssertElementExistsAtPath(sqsStatement, "Action");
            var actionsList = actions.EnumerateArray().Select(a => a.GetString()).ToList();
            Assert.Contains("sqs:ReceiveMessage", actionsList);
            Assert.Contains("sqs:DeleteMessage", actionsList);
            Assert.Contains("sqs:GetQueueAttributes", actionsList);

            // Validate IAM Policy references SQS Queue ARN
            var policyResource = AssertElementExistsAtPath(sqsStatement, "Resource/Fn::GetAtt");
            var policyResourceArray = policyResource.EnumerateArray().ToList();
            Assert.Equal(sqsQueue.LogicalId, policyResourceArray[0].GetString());
            Assert.Equal("Arn", policyResourceArray[1].GetString());

            // Validate Lambda depends on IAM Role and Policy
            var lambdaDependsOn = AssertElementExistsAtPath(lambdaFunction.Resource, "DependsOn");
            Assert.Equal(JsonValueKind.Array, lambdaDependsOn.ValueKind);
            var dependsOnArray = lambdaDependsOn.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains(lambdaRole.LogicalId, dependsOnArray);
            Assert.Contains(sqsPolicy.LogicalId, dependsOnArray);

            return Task.CompletedTask;
        };

        await ExecutePublishAsync(nameof(Scenarios.PublishLambdaWithCustomization), cloudFormationValidation);
    }

    [Fact]
    public async Task TestPublishLambdaWithReferences()
    {
        var cloudFormationValidation = (JsonDocument cfTemplateDoc) =>
        {
            // Validate Lambda Function exists
            var lambdaFunctions = GetResourcesOfType(cfTemplateDoc, "AWS::Lambda::Function");
            Assert.Single(lambdaFunctions);
            var lambdaFunction = lambdaFunctions[0];

            // Validate Lambda has NO VpcConfig (not attached to VPC)
            var vpcConfig = GetElementAtPath(lambdaFunction.Resource, "Properties/VpcConfig");
            Assert.Null(vpcConfig);

            // Validate Lambda has Environment Variables with WebApp1 reference
            var lambdaEnvVars = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Environment/Variables");
            Assert.Equal(JsonValueKind.Object, lambdaEnvVars.ValueKind);

            // Validate Lambda environment variable references WebApp1's endpoint
            var webApp1EnvVarFnJoin = AssertElementExistsAtPath(lambdaFunction.Resource, "Properties/Environment/Variables/services__WebApp1__https__0/Fn::Join");
            AssertJsonEquals("""
            [
             "",
             [
              "https://",
              {
               "Fn::GetAtt": [
                "ProjectWebApp1",
                "Endpoint"
               ]
              },
              "/"
             ]
            ]
            """, webApp1EnvVarFnJoin);

            // Validate Lambda depends on WebApp1
            var lambdaDependsOn = AssertElementExistsAtPath(lambdaFunction.Resource, "DependsOn");
            Assert.Equal(JsonValueKind.Array, lambdaDependsOn.ValueKind);
            var dependsOnArray = lambdaDependsOn.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains("ProjectWebApp1", dependsOnArray);

            return Task.CompletedTask;
        };

        await ExecutePublishAsync(nameof(Scenarios.PublishLambdaWithReferences), cloudFormationValidation);
    }


    private async Task ExecutePublishAsync(string scenario, Func<JsonDocument, Task> cfTemplateValidation)
    {
        var outputPath = GetTempOutputPath();
        try
        {
            var args = GetPublishArguments(outputPath, nameof(Scenarios.PublishWebApp2ReferenceOnWebApp1));
            var mockAwsEnvironmentService = new MockAWSEnvironmentService(args);
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DeploymentTestApp_AppHost>(GetPublishArguments(outputPath, scenario));
            appHost.Services.AddSingleton<IAWSEnvironmentService>(mockAwsEnvironmentService);

            // To speed up tests we don't actually need to build the projects since we are only validating the generated CF template.
            // This stubs out the service interfaces that are used for building projects.
            appHost.Services.AddSingleton<ITarballContainerImageBuilder, MockTarballContainerImageBuilder>();
            appHost.Services.AddSingleton<IResourceContainerImageBuilder, MockResourceContainerImageBuilder>();
            appHost.Services.AddSingleton<ILambdaDeploymentPackager, MockLambdaDeploymentPackager>();

            await using var app = await appHost.BuildAsync();
            await app.RunAsync(cts.Token);

            var cfTemplatePath = Path.Combine(outputPath, "cdk.out", $"{scenario}.template.json");
            Assert.True(File.Exists(cfTemplatePath));

            var cfTemplateDoc = JsonDocument.Parse(File.ReadAllText(cfTemplatePath));

            await cfTemplateValidation(cfTemplateDoc);
        }
        finally
        {
            try
            {
                if (Directory.Exists(outputPath))
                    Directory.Delete(outputPath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete temp output path {outputPath}: {ex}");
            }
        }
    }


    private static string[] GetPublishArguments(string outputPath, string scenario) => new string[] { "--publisher", "default", "--output-path", outputPath, DeploymentTestAppConstants.ScenarioSwitch, scenario, "--no-aws-deploy" };

    string GetTempOutputPath() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    internal class MockAWSEnvironmentService(string[] args) : IAWSEnvironmentService
    {
        public string[] GetCommandLineArgs() => args;
    }

    internal class MockTarballContainerImageBuilder : ITarballContainerImageBuilder
    {
        public Task<string> CreateTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken = default)
        {
            // Return a dummy tarball path
            var filePath = Path.Combine(Path.GetTempPath(), $"{resource}_image.tar");
            File.WriteAllText(filePath, "dummy content");
            return Task.FromResult(filePath);
        }
    }

    internal class MockResourceContainerImageBuilder : IResourceContainerImageBuilder
    {
        public Task BuildImageAsync(IResource resource, ContainerBuildOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task BuildImagesAsync(IEnumerable<IResource> resources, ContainerBuildOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PushImageAsync(string imageName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TagImageAsync(string localImageName, string targetImageName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    internal class MockLambdaDeploymentPackager : ILambdaDeploymentPackager
    {

        public Task<LambdaDeploymentPackagerResult> CreateDeploymentPackageAsync(LambdaProjectResource lambdaFunction, string outputDirectory, CancellationToken cancellationToken)
        {
            // Return a dummy package path
            var filePath = Path.Combine(Path.GetTempPath(), $"{lambdaFunction.Name}_lambda_package.zip");
            File.WriteAllText(filePath, "dummy content");
            return Task.FromResult(new LambdaDeploymentPackagerResult { LocalLocation = filePath, Success = true});
        }
    }
}
