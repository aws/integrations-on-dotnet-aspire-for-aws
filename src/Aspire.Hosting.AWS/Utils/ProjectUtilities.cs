using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Utils;

internal static class ProjectUtilities
{
    private const string LaunchSettingsFile = "launchSettings.json";
    private const string LaunchSettingsNodePrefix = "Aspire_";
    
    /// <summary>
    /// Initializes the project's launch settings if necessary, and
    /// ensures they are referencing the tester tool's location.
    /// </summary>
    public static void UpdateLaunchSettingsWithLambdaTester(
        string resourceName, 
        string functionHandler, 
        string assemblyName, 
        string projectPath, 
        string runtimeSupportAssemblyPath, 
        string targetFramework)
    {
        
        // Retrieve the current launch settings JSON from wherever it's stored.
        string launchSettingsJson = GetLaunchSettings(projectPath);

        // Parse the JSON into a mutable JsonNode (root is expected to be an object)
        JsonNode? rootNode = JsonNode.Parse(launchSettingsJson);
        if (rootNode is not JsonObject root)
        {
            // If the parsed JSON isn’t an object, initialize a new one (or handle the error as needed)
            root = new JsonObject();
        }

        // Get (or create) the "profiles" JSON object
        JsonObject profiles = root["profiles"]?.AsObject() ?? new JsonObject();
        root["profiles"] = profiles;  // Ensure it's added to the root

        // Use a constant for the launch settings node key (ensure LAUNCH_SETTINGS_NODE is defined)
        var launchSettingsNodeKey = $"{LaunchSettingsNodePrefix}{resourceName}";

        // Get (or create) the specific profile for Lambda Tester
        JsonObject? lambdaTester = profiles[launchSettingsNodeKey]?.AsObject();
        if (lambdaTester == null)
        {
            lambdaTester = new JsonObject
            {
                ["commandName"] = "Executable",
                ["commandLineArgs"] = $"exec --depsfile ./{assemblyName}.deps.json --runtimeconfig ./{assemblyName}.runtimeconfig.json {SubstituteHomePath(runtimeSupportAssemblyPath)} {functionHandler}"
            };

            profiles[launchSettingsNodeKey] = lambdaTester;
        }

        // Update or add properties as needed
        lambdaTester["workingDirectory"] = $".\\bin\\$(Configuration)\\{targetFramework}";
        lambdaTester["executablePath"] = "dotnet";

        // Serialize the updated JSON with indentation
        var options = new JsonSerializerOptions { WriteIndented = true };
        string updatedJson = root.ToJsonString(options);

        // Save the updated JSON back to the launch settings file.
        SaveLaunchSettings(projectPath, updatedJson);
    }
    
    /// <summary>
    /// Initializes the project's launch settings if necessary, and
    /// ensures they are referencing the tester tool's location.
    /// </summary>
    public static void UpdateLaunchSettingsEndpoint(
        string profileName, 
        string endpoint,
        string projectPath)
    {
        
        // Retrieve the current launch settings JSON from wherever it's stored.
        string launchSettingsJson = GetLaunchSettings(projectPath);

        // Parse the JSON into a mutable JsonNode (root is expected to be an object)
        JsonNode? rootNode = JsonNode.Parse(launchSettingsJson);
        if (rootNode is not JsonObject root)
        {
            // If the parsed JSON isn’t an object, initialize a new one (or handle the error as needed)
            root = new JsonObject();
        }

        // Get (or create) the "profiles" JSON object
        JsonObject profiles = root["profiles"]?.AsObject() ?? new JsonObject();
        root["profiles"] = profiles;  // Ensure it's added to the root

        // Get (or create) the specific profile for Lambda Tester
        var lambdaTester = profiles[profileName]?.AsObject();
        if (lambdaTester is null)
            throw new Exception("");
        var environmentVariables = lambdaTester?["environmentVariables"]?.AsObject();
        if (environmentVariables is null)
            throw new Exception("");
        environmentVariables = new JsonObject
        {
            ["AWS_LAMBDA_RUNTIME_API"] = endpoint
        };
        lambdaTester!["environmentVariables"] = environmentVariables;
        
        // Serialize the updated JSON with indentation
        var options = new JsonSerializerOptions { WriteIndented = true };
        string updatedJson = root.ToJsonString(options);

        // Save the updated JSON back to the launch settings file.
        SaveLaunchSettings(projectPath, updatedJson);
    }

    private static string SubstituteHomePath(string path)
    {
        var userProfileEnvironmentVariable = "%USERPROFILE%";
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            userProfileEnvironmentVariable = "$HOME";
        }

        if (path.StartsWith(userProfilePath))
        {
            return path.Replace(userProfilePath, userProfileEnvironmentVariable);
        }

        return path;
    }
    
    private static string GetLaunchSettings(string projectPath)
    {
        var parentDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(parentDirectory))
            throw new ArgumentException($"The project path '{projectPath}' is invalid. Unable to retrieve the '{LaunchSettingsFile}' file.");
        var properties = Path.Combine(parentDirectory, "Properties");
        if (!Directory.Exists(properties))
        {
            Directory.CreateDirectory(properties);
            return "{}";
        }

        var fullPath = Path.Combine(properties, LaunchSettingsFile);
        if (!File.Exists(fullPath))
            return "{}";

        return File.ReadAllText(fullPath);
    }

    private static void SaveLaunchSettings(string projectPath, string content)
    {
        var parentDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(parentDirectory))
            throw new ArgumentException($"The project path '{projectPath}' is invalid. Unable to retrieve the '{LaunchSettingsFile}' file.");
        var fullPath = Path.Combine(parentDirectory, "Properties", LaunchSettingsFile);
        File.WriteAllText(fullPath, content);
    }
    
    public static async Task<string> GetProjectAssemblyNameAsync(IProcessCommandService processCommandService, ILogger<LambdaEmulatorResource> logger, string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            var results = await processCommandService.RunProcessAndCaptureOuputAsync(logger, "dotnet", $"msbuild \"{projectPath}\" -nologo -v:q -getProperty:AssemblyName", cancellationToken);
            if (results.ExitCode != 0)
            {
                return string.Empty;
            }

            logger.LogDebug("The assembly name of '{projectPath}' is {assemblyName}", projectPath, results.Output);
            return results.Output;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error retrieving the assembly name of '{projectPath}'", projectPath);
            return string.Empty;
        }
    }

    public static async Task<string> GetProjectTargetFrameworkAsync(IProcessCommandService processCommandService, ILogger<LambdaEmulatorResource> logger, string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            var results = await processCommandService.RunProcessAndCaptureOuputAsync(logger, "dotnet", $"msbuild \"{projectPath}\" -nologo -v:q -getProperty:TargetFramework", cancellationToken);
            if (results.ExitCode != 0)
            {
                return string.Empty;
            }

            logger.LogDebug("The target framework of '{projectPath}' is {targetFramework}", projectPath, results.Output);
            return results.Output;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error retrieving the target framework of '{projectPath}'", projectPath);
            return string.Empty;
        }
    }
}