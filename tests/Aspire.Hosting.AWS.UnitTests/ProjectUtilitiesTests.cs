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
        string outputPath = $"bin/Debug/{targetFramework}";

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
            targetFramework,
            outputPath);

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
            : runtimeSupportAssemblyPath.Replace(userProfile, "$(HOME)");

        Assert.Contains(expectedRuntimePath, commandLineArgs);
        Assert.Contains(functionHandler, commandLineArgs);

        // Verify the workingDirectory was set correctly.
        string workingDirectory = profile["workingDirectory"]?.GetValue<string>() ?? "";
        string expectedWorkingDir = Path.Combine("bin", "Debug", targetFramework).Replace("\\", "/");
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
        string outputPath = $"bin/Debug/{targetFramework}";

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
            targetFramework,
            outputPath);

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
        string expectedWorkingDir = Path.Combine("bin", "Debug", targetFramework).Replace("\\", "/");
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
        string outputPath = $"bin/Debug/{targetFramework}";
        string runtimeSupportAssemblyPath = @"C:\dummy.dll";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ProjectUtilities.UpdateLaunchSettingsWithLambdaTester(
                resourceName,
                functionHandler,
                assemblyName,
                invalidProjectPath,
                runtimeSupportAssemblyPath,
                targetFramework,
                outputPath));
    }

    [Fact]
    public void ResolveCanonicalPath_ResolvesSymlinkedSegments()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Creating symlinks on Windows requires elevated privileges.
            return;
        }

        // Arrange
        string target = Path.Combine(_tempDirectory, "target");
        Directory.CreateDirectory(Path.Combine(target, "sub"));
        string link = Path.Combine(_tempDirectory, "link");
        Directory.CreateSymbolicLink(link, target);

        // Act
        string result = ProjectUtilities.ResolveCanonicalPath(Path.Combine(link, "sub"));

        // Assert
        Assert.Equal(ProjectUtilities.ResolveCanonicalPath(Path.Combine(target, "sub")), result);
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}link{Path.DirectorySeparatorChar}", result + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void ResolveCanonicalPath_IsNoOp_ForPathWithoutSymlinks()
    {
        // Arrange
        string path = Path.Combine(_tempDirectory, "does", "not", "exist");

        // Act
        string result = ProjectUtilities.ResolveCanonicalPath(path);

        // Assert: non-existent segments are kept as-is; existing segments may canonicalize (e.g. /var -> /private/var on macOS).
        Assert.EndsWith(Path.Combine("does", "not", "exist"), result);
    }

    [Fact]
    public void CreateExecutableWrapperProject_ReturnsCanonicalPath()
    {
        // Arrange
        string classLibraryProjectPath = GetTempProjectPath();

        // Act
        string wrapperProjectPath = ProjectUtilities.CreateExecutableWrapperProject(classLibraryProjectPath, "TestNamespace.Function::Handler", "net8.0");

        try
        {
            // Assert: the generated project path must contain no symlinked segments,
            // otherwise NuGet restore and MSBuild disagree on relative ProjectReference paths (MSB3202 on macOS).
            Assert.True(File.Exists(wrapperProjectPath));
            Assert.Equal(ProjectUtilities.ResolveCanonicalPath(wrapperProjectPath), wrapperProjectPath);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(wrapperProjectPath)!, true);
        }
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
        string outputPath = $"bin/Debug/{targetFramework}";
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
            targetFramework,
            outputPath);

        // Assert
        string jsonContent = File.ReadAllText(launchSettingsPath);
        JsonNode? rootNode = JsonNode.Parse(jsonContent);
        Assert.NotNull(rootNode);
        Assert.IsType<JsonObject>(rootNode);
    }
}