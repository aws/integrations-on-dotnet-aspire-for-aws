using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Utils;

internal static class ProjectUtilities
{
    /// <summary>
    /// Initializes the project's launch settings if necessary, and
    /// ensures they are referencing the Amazon.Lambda.TestTool's location.
    /// </summary>
    /// <param name="resourceName">Lambda function name to be used part of the launch setting profile</param>
    /// <param name="functionHandler">Lambda function handler</param>
    /// <param name="assemblyName">Assembly name of the Lambda function to retrieve the deps file and runtime config file</param>
    /// <param name="projectPath">Project file path</param>
    /// <param name="runtimeSupportAssemblyPath">Runtime Support dll path</param>
    /// <param name="targetFramework">Lambda function target framework</param>
    /// <param name="logger">A logger instance</param>
    public static void UpdateLaunchSettingsWithLambdaTester(
        string resourceName, 
        string functionHandler, 
        string assemblyName, 
        string projectPath, 
        string runtimeSupportAssemblyPath, 
        string targetFramework,
        ILogger? logger = null)
    {
        try
        {
            // Retrieve the current launch settings JSON from wherever it's stored.
            string launchSettingsJson = GetLaunchSettings(projectPath);

            // Parse the JSON into a mutable JsonNode (root is expected to be an object)
            JsonNode? rootNode = JsonNode.Parse(launchSettingsJson);
            if (rootNode is not JsonObject root)
            {
                // If the parsed JSON isn’t an object, initialize a new one.
                root = new JsonObject();
            }

            // Get or create the "profiles" JSON object
            JsonObject profiles = root["profiles"]?.AsObject() ?? new JsonObject();
            root["profiles"] = profiles;  // Ensure it's added to the root

            var launchSettingsNodeKey = $"{Constants.LaunchSettingsNodePrefix}{resourceName}";

            // Get or create the specific profile for Amazon.Lambda.TestTool
            JsonObject? lambdaTester = profiles[launchSettingsNodeKey]?.AsObject();
            if (lambdaTester == null)
            {
                lambdaTester = new JsonObject
                {
                    ["commandName"] = "Executable",
                    ["executablePath"] = "dotnet"
                };

                profiles[launchSettingsNodeKey] = lambdaTester;
            }

            // Update properties that contain a path that is environment-specific
            lambdaTester["commandLineArgs"] =
                $"exec --depsfile ./{assemblyName}.deps.json --runtimeconfig ./{assemblyName}.runtimeconfig.json {SubstituteHomePath(runtimeSupportAssemblyPath)} {functionHandler}";
            lambdaTester["workingDirectory"] = Path.Combine(".", "bin", "$(Configuration)", targetFramework);

            // Serialize the updated JSON with indentation
            var options = new JsonSerializerOptions { WriteIndented = true };
            string updatedJson = root.ToJsonString(options);

            // Save the updated JSON back to the launch settings file.
            SaveLaunchSettings(projectPath, updatedJson);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Failed to parse the launchSettings.json file for the project '{ProjectPath}'.", projectPath);
        }
    }
    
    /// <summary>
    /// Check if a path is in the user profile directory and use the environment-specific environment variable.
    /// </summary>
    /// <param name="path">The path to update to use the user profile environment variable</param>
    /// <returns>A path that uses the user profile environment variable</returns>
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
    
    /// <summary>
    /// Retrieve a project's launchSettings.json file contents
    /// </summary>
    /// <param name="projectPath">The project file path</param>
    /// <returns>The launchSetting.json content</returns>
    private static string GetLaunchSettings(string projectPath)
    {
        var parentDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(parentDirectory))
            throw new ArgumentException($"The project path '{projectPath}' is invalid. Unable to retrieve the '{Constants.LaunchSettingsFile}' file.");
        var properties = Path.Combine(parentDirectory, "Properties");
        if (!Directory.Exists(properties))
        {
            return "{}";
        }

        var fullPath = Path.Combine(properties, Constants.LaunchSettingsFile);
        if (!File.Exists(fullPath))
            return "{}";

        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Write the launchSettings.json content to disk
    /// </summary>
    /// <param name="projectPath">The project file path</param>
    /// <param name="content">The launchSettings.json content</param>
    private static void SaveLaunchSettings(string projectPath, string content)
    {
        var parentDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(parentDirectory))
            throw new ArgumentException($"The project path '{projectPath}' is invalid. Unable to retrieve the '{Constants.LaunchSettingsFile}' file.");
        var properties = Path.Combine(parentDirectory, "Properties");
        if (!Directory.Exists(properties))
        {
            Directory.CreateDirectory(properties);
        }
        var fullPath = Path.Combine(properties, Constants.LaunchSettingsFile);
        File.WriteAllText(fullPath, content);
    }

    /// <summary>
    /// Create an executable wrapper project that invokes the specified Lambda function.
    /// </summary>
    /// <param name="classLibraryProjectPath">The project path of the class library Lambda function</param>
    /// <param name="lambdaHandler">The Lambda function handler</param>
    /// <param name="targetFramework">The Lambda project target framework</param>
    /// <returns>A project file path of the executable wrapper project</returns>
    public static string CreateExecutableWrapperProject(string classLibraryProjectPath, string lambdaHandler, string targetFramework)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        var projectContent = $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>{targetFramework}</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Amazon.Lambda.RuntimeSupport"" Version=""{Constants.RuntimeSupportPackageVersion}"" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""{classLibraryProjectPath}"" />
  </ItemGroup>
</Project>
";
        var projectName = $"Wrapper{Path.GetFileName(classLibraryProjectPath)}";
        var projectPath = Path.Combine(tempPath, projectName);
        File.WriteAllText(projectPath, projectContent);
                
        string programContent = $@"
using Amazon.Lambda.RuntimeSupport;

RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(""{lambdaHandler}"");
await runtimeSupportInitializer.RunLambdaBootstrap();
";
        var programPath = Path.Combine(tempPath, "Program.cs");
        File.WriteAllText(programPath, programContent);

        return projectPath;
    }
}