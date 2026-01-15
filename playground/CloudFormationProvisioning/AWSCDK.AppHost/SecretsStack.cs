// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.SecretsManager;
using Constructs;

namespace AWSCDK.AppHost;

public class SecretsStack : Stack
{
    public ISecret DatabaseCredentials { get; }
    
    public ISecret ApiKey { get; }
    
    public ISecret ApplicationSecret { get; }

    public SecretsStack(Construct scope, string id)
        : base(scope, id)
    {
        // Database credentials with auto-generated password
        DatabaseCredentials = new Secret(this, "DatabaseCredentials", new SecretProps
        {
            Description = "Database connection credentials",
            GenerateSecretString = new SecretStringGenerator
            {
                SecretStringTemplate = "{\"username\":\"dbadmin\"}",
                GenerateStringKey = "password",
                PasswordLength = 32,
                ExcludeCharacters = "\"@/\\"
            }
        });

        // API key for external service integration
        ApiKey = new Secret(this, "ApiKey", new SecretProps
        {
            Description = "External API authentication key",
            GenerateSecretString = new SecretStringGenerator
            {
                PasswordLength = 64,
                ExcludePunctuation = true
            }
        });

        // Application secret for JWT signing
        ApplicationSecret = new Secret(this, "AppSecret", new SecretProps
        {
            Description = "Application JWT signing secret",
            GenerateSecretString = new SecretStringGenerator
            {
                PasswordLength = 128,
                IncludeSpace = false
            }
        });
    }
}
