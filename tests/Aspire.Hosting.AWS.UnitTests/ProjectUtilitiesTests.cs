using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Aspire.Hosting.AWS.Utils;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class ProjectUtilitiesTests : IDisposable
{
    private readonly string _tempDirectory;

    public ProjectUtilitiesTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// Returns a temp project file path inside the temp directory.
    /// </summary>
    private string GetTempProjectPath()
    {
        // Create a dummy project file in the temp directory.
        string projectFile = Path.Combine(_tempDirectory, "TestProject.csproj");
        File.WriteAllText(projectFile, "<Project></Project>");
        return projectFile;
    }

    [Fact]
    public void UpdateLaunchSettings_CreatesNewLaunchSettingsFile_WhenNoneExists()
    {
        // Arrange
        string projectPath = GetTempProjectPath();
        string resourceName = "TestResource";
        string functionHandler = "TestNamespace.Function::Handler";
        string assemblyName = "TestAssembly";
        string targetFramework = "net8.0";

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string runtimeSupportAssemblyPath = Path.Combine(userProfile, "dummy.dll");

        string propertiesDir = Path.Combine(Path.GetDirectoryName(projectPath)!, "Properties");
        string launchSettingsPath = Path.Combine(propertiesDir, Constants.LaunchSettingsFile);
        Assert.False(Directory.Exists(propertiesDir));

        // Act
        ProjectUtilities.UpdateLaunchSettingsWithLambdaTester(
            resourceName,
            functionHandler,
            assemblyName,
            projectPath,
            runtimeSupportAssemblyPath,
            targetFramework);

        // Assert
        Assert.True(Directory.Exists(propertiesDir));
        Assert.True(File.Exists(launchSettingsPath));

        string jsonContent = File.ReadAllText(launchSettingsPath);
        JsonNode? rootNode = JsonNode.Parse(jsonContent);
        Assert.NotNull(rootNode);
        JsonObject root = Assert.IsType<JsonObject>(rootNode);

        Assert.True(root.TryGetPropertyValue("profiles", out JsonNode? profilesNode));
        JsonObject profiles = Assert.IsType<JsonObject>(profilesNode);
        
        string expectedProfileKey = $"{Constants.LaunchSettingsNodePrefix}{resourceName}";
        Assert.True(profiles.TryGetPropertyValue(expectedProfileKey, out JsonNode? profileNode));
        JsonObject profile = Assert.IsType<JsonObject>(profileNode);

        Assert.Equal("Executable", profile["commandName"]?.GetValue<string>());
        Assert.Equal("dotnet", profile["executablePath"]?.GetValue<string>());

        // Check the commandLineArgs property includes substituted home path.
        string commandLineArgs = profile["commandLineArgs"]?.GetValue<string>() ?? "";
        string expectedRuntimePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? runtimeSupportAssemblyPath.Replace(userProfile, "%USERPROFILE%")
            : runtimeSupportAssemblyPath.Replace(userProfile, "$HOME");

        Assert.Contains(expectedRuntimePath, commandLineArgs);
        Assert.Contains(functionHandler, commandLineArgs);

        // Verify the workingDirectory was set correctly.
        string workingDirectory = profile["workingDirectory"]?.GetValue<string>() ?? "";
        string expectedWorkingDir = Path.Combine(".", "bin", "$(Configuration)", targetFramework);
        Assert.Equal(expectedWorkingDir, workingDirectory);
    }

    [Fact]
    public void UpdateLaunchSettings_UpdatesExistingLaunchSettingsFile_WithExistingProfiles()
    {
        // Arrange
        string projectPath = GetTempProjectPath();
        string propertiesDir = Path.Combine(Path.GetDirectoryName(projectPath)!, "Properties");
        Directory.CreateDirectory(propertiesDir);

        string launchSettingsPath = Path.Combine(propertiesDir, Constants.LaunchSettingsFile);
        File.WriteAllText(launchSettingsPath, "{ \"profiles\": {} }");

        string resourceName = "ExistingResource";
        string functionHandler = "ExistingNamespace.Handler::Run";
        string assemblyName = "ExistingAssembly";
        string targetFramework = "net8.0";

        string runtimeSupportAssemblyPath = @"C:\path\to\support.dll";
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            runtimeSupportAssemblyPath = "/path/to/support.dll";
        }

        // Act
        ProjectUtilities.UpdateLaunchSettingsWithLambdaTester(
            resourceName,
            functionHandler,
            assemblyName,
            projectPath,
            runtimeSupportAssemblyPath,
            targetFramework);

        // Assert
        string jsonContent = File.ReadAllText(launchSettingsPath);
        JsonNode? rootNode = JsonNode.Parse(jsonContent);
        Assert.NotNull(rootNode);
        JsonObject root = Assert.IsType<JsonObject>(rootNode);

        Assert.True(root.TryGetPropertyValue("profiles", out JsonNode? profilesNode));
        JsonObject profiles = Assert.IsType<JsonObject>(profilesNode);

        string expectedProfileKey = $"{Constants.LaunchSettingsNodePrefix}{resourceName}";
        Assert.True(profiles.TryGetPropertyValue(expectedProfileKey, out JsonNode? profileNode));
        JsonObject profile = Assert.IsType<JsonObject>(profileNode);

        Assert.Equal("Executable", profile["commandName"]?.GetValue<string>());
        Assert.Equal("dotnet", profile["executablePath"]?.GetValue<string>());

        string commandLineArgs = profile["commandLineArgs"]?.GetValue<string>() ?? "";
        Assert.Contains(runtimeSupportAssemblyPath, commandLineArgs);
        Assert.Contains(functionHandler, commandLineArgs);

        string workingDirectory = profile["workingDirectory"]?.GetValue<string>() ?? "";
        string expectedWorkingDir = Path.Combine(".", "bin", "$(Configuration)", targetFramework);
        Assert.Equal(expectedWorkingDir, workingDirectory);
    }

    [Fact]
    public void UpdateLaunchSettings_ThrowsArgumentException_ForInvalidProjectPath()
    {
        // Arrange
        string invalidProjectPath = "invalid.csproj";
        string resourceName = "Test";
        string functionHandler = "TestNamespace.Function::Handler";
        string assemblyName = "TestAssembly";
        string targetFramework = "net8.0";
        string runtimeSupportAssemblyPath = @"C:\dummy.dll";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ProjectUtilities.UpdateLaunchSettingsWithLambdaTester(
                resourceName,
                functionHandler,
                assemblyName,
                invalidProjectPath,
                runtimeSupportAssemblyPath,
                targetFramework));
    }

    [Fact]
    public void UpdateLaunchSettings_ReplacesMalformedLaunchSettingsJson_WithNewObject()
    {
        // Arrange
        string projectPath = GetTempProjectPath();
        string propertiesDir = Path.Combine(Path.GetDirectoryName(projectPath)!, "Properties");
        Directory.CreateDirectory(propertiesDir);

        string launchSettingsPath = Path.Combine(propertiesDir, Constants.LaunchSettingsFile);
        File.WriteAllText(launchSettingsPath, "[ ]");

        string resourceName = "Malformed";
        string functionHandler = "MalformedNamespace.Handler::Invoke";
        string assemblyName = "MalformedAssembly";
        string targetFramework = "net8.0";
        string runtimeSupportAssemblyPath = @"C:\malformed.dll";
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            runtimeSupportAssemblyPath = "/malformed.dll";
        }

        // Act
        ProjectUtilities.UpdateLaunchSettingsWithLambdaTester(
            resourceName,
            functionHandler,
            assemblyName,
            projectPath,
            runtimeSupportAssemblyPath,
            targetFramework);

        // Assert
        string jsonContent = File.ReadAllText(launchSettingsPath);
        JsonNode? rootNode = JsonNode.Parse(jsonContent);
        Assert.NotNull(rootNode);
        Assert.IsType<JsonObject>(rootNode);
    }
}