using Amazon.SSO.Model;
using Aspire.Hosting.AWS.Utils.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aspire.Hosting.AWS.Utils;

internal class SystemCapabilityEvaluator
{
    private const string NODEJS_DEPENDENCY_NAME = "Node.js";
    private const string NODEJS_INSTALLATION_URL = "https://nodejs.org/en/download/";

    private static readonly Version MinimumNodeJSVersion = new Version(20, 0, 0);

    /// <summary>
    /// How long to wait for the commands we run to determine if Node/Docker/etc. are installed to finish
    /// </summary>
    private const int CAPABILITY_EVALUATION_TIMEOUT_MS = 60000; // one minute

    /// <summary>
    /// Attempt to determine the installed Node.js version
    /// </summary>
    private static async Task<NodeInfo> GetNodeJsVersionAsync()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(CAPABILITY_EVALUATION_TIMEOUT_MS);

        try
        {
            // run node --version to get the version
            var commandService = new ProcessCommandService();
            var result = await commandService.RunProcessAndCaptureOutputAsync(null, "node", "--version", Environment.CurrentDirectory, cancellationTokenSource.Token);

            var versionString = result.Output;

            if (versionString.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                versionString = versionString.Substring(1, versionString.Length - 1);

            Version? version = null;
            if (result.ExitCode != 0 || !Version.TryParse(versionString, out version))
                return new NodeInfo(null);

            return new NodeInfo(version);

        }
        catch (TaskCanceledException)
        {
            // If the check timed out, treat Node as not installed
            return new NodeInfo(null);
        }
    }

    public static async Task CheckNodeInstallationAsync()
    {
        var info = await GetNodeJsVersionAsync();

        if (info == null)
            throw new InvalidOperationException($"Missing Node.js installation. Node.js {MinimumNodeJSVersion} is required for working with AWS CDK.");

        if (info.NodeJsVersion < MinimumNodeJSVersion)
            throw new InvalidOperationException($"Installed Node.js version {info.NodeJsVersion} doesn't meet the requirement for AWS CDK which requires a minimum version {MinimumNodeJSVersion}.");
    }

    public static bool IsCDKInstalled()
    {
        try
        {
            // run node --version to get the version
            var commandService = new ProcessCommandService();
            var result = commandService.RunCDKProcess(null, Microsoft.Extensions.Logging.LogLevel.Information, "--help", Environment.CurrentDirectory);

            return result.ExitCode == 0;

        }
        catch (Exception)
        {
            return false;
        }
    }

    internal class NodeInfo
    {
        /// <summary>
        /// Version of Node if it's installed, else null if not detected
        /// </summary>
        public Version? NodeJsVersion { get; set; }

        public NodeInfo(Version? version) => NodeJsVersion = version;
    }
}
