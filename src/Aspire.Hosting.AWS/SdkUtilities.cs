// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Reflection;
using System.Text;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Aspire.Hosting.ApplicationModel;
using Amazon.Runtime.CredentialManagement;

namespace Aspire.Hosting.AWS;
internal static class SdkUtilities
{
    private const string UserAgentHeader = "User-Agent";
    private static string? s_userAgentHeader;

    private static string GetUserAgentStringSuffix()
    {
        if (s_userAgentHeader == null)
        {
            var builder = new StringBuilder($"lib/aspire.hosting.aws#{GetAssemblyVersion()}");
            s_userAgentHeader = builder.ToString();
        }

        return s_userAgentHeader;
    }

    internal static string GetAssemblyVersion()
    {
        var attribute = typeof(AWSLifecycleHook).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute != null ? attribute.InformationalVersion.Split('+')[0] : "Unknown";
    }

    internal static void ConfigureUserAgentString(object sender, RequestEventArgs e)
    {
        var suffix = GetUserAgentStringSuffix();
        if (e is not WebServiceRequestEventArgs args || !args.Headers.TryGetValue(UserAgentHeader, out var currentValue) || currentValue.Contains(suffix))
        {
            return;
        }

        args.Headers[UserAgentHeader] = currentValue + " " + suffix;
    }

    internal static void ApplySDKConfig(EnvironmentCallbackContext context, IAWSSDKConfig awsSdkConfig, bool force)
    {
        if (context.Logger != null)
        {
            // To help debugging do a validation of the SDK config. The results will be logged.
            BackgroundSDKConfigValidation(context.Logger, awsSdkConfig);
        }

        if (!string.IsNullOrEmpty(awsSdkConfig.Profile))
        {
            if (force || !context.EnvironmentVariables.ContainsKey("AWS__Profile"))
            {
                // The environment variable that AWSSDK.Extensions.NETCore.Setup will look for via IConfiguration.
                context.EnvironmentVariables["AWS__Profile"] = awsSdkConfig.Profile;

                // The environment variable the service clients look for service clients created without AWSSDK.Extensions.NETCore.Setup.
                context.EnvironmentVariables["AWS_PROFILE"] = awsSdkConfig.Profile;
            }
        }

        if (awsSdkConfig.Region != null)
        {
            if (force || !context.EnvironmentVariables.ContainsKey("AWS__Region"))
            {
                // The environment variable that AWSSDK.Extensions.NETCore.Setup will look for via IConfiguration.
                context.EnvironmentVariables["AWS__Region"] = awsSdkConfig.Region.SystemName;

                // The environment variable the service clients look for service clients created without AWSSDK.Extensions.NETCore.Setup.
                context.EnvironmentVariables["AWS_REGION"] = awsSdkConfig.Region.SystemName;
            }
        }
    }

    internal static void BackgroundSDKDefaultConfigValidation(ILogger logger)
    {
        var chain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
        var profiles = chain.ListProfiles();

        // If there are no profiles then the developer is unlikely connecting in the dev environment
        // to AWS with the SDK. In this case it doesn't make sense to validate credentials.
        if (profiles.Count == 0)
            return;

        // If there is not a default profile then skip validating it.
        var defaultProfile = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_PROFILE")) ? Environment.GetEnvironmentVariable("AWS_PROFILE") : SharedCredentialsFile.DefaultProfileName;
        if (profiles.FirstOrDefault(x => string.Equals(defaultProfile, x.Name)) == null)
            return;

        _ = ValidateSdkConfigAsync(logger, new AWSSDKConfig(), true);
    }

    internal static void BackgroundSDKConfigValidation(ILogger logger, IAWSSDKConfig config)
    {
        _ = ValidateSdkConfigAsync(logger, config, false);
    }

    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    static HashSet<string> _validatedSdkConfigs = new HashSet<string>();
    private static async Task ValidateSdkConfigAsync(ILogger logger, IAWSSDKConfig config, bool defaultConfig)
    {
        // Cache key used to make sure we only validate a SDK configuration once.
        var cacheKey = $"Profile:{config.Profile},Region:{config.Region?.SystemName}";

        await _semaphore.WaitAsync();
        try
        {
            if (_validatedSdkConfigs.Contains(cacheKey))
            {
                return;
            }

            var stsConfig = new AmazonSecurityTokenServiceConfig();
            if (config.Region != null)
                stsConfig.RegionEndpoint = config.Region;
            if (!string.IsNullOrEmpty(config.Profile))
                stsConfig.Profile = new Amazon.Profile(config.Profile);

            try
            {
                using var stsClient = new AmazonSecurityTokenServiceClient(stsConfig);
                stsClient.BeforeRequestEvent += ConfigureUserAgentString;

                // Make an AWS call to an API that doesn't require permissions to confirm
                // the sdk config is able to connect to AWS.
                var response = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());

                if (defaultConfig)
                    logger.LogInformation("Default AWS SDK config validated for account: {accountId}", response.Account);
                else
                    logger.LogInformation("AWS SDK config validated for account: {accountId}", response.Account);

                _validatedSdkConfigs.Add(cacheKey);
            }
            catch (Exception)
            {
                if (defaultConfig)
                {
                    logger.LogWarning("Failed to connect to AWS using default AWS SDK config");
                }
                else
                {
                    logger.LogError("Failed to connect to AWS using AWS SDK config: {configSettings}", cacheKey);
                }

                _validatedSdkConfigs.Add(cacheKey);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
