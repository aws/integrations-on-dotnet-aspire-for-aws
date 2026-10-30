// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Deployment;

internal static class CDKOutputDirectory
{
    internal static string Determine(string[] args)
    {
        string? outputPath = null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--output-path", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[i], "-o", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = args[i + 1];
            }
        }

        outputPath ??= Environment.CurrentDirectory;
        if (!string.Equals(new DirectoryInfo(outputPath).Name, "cdk.out", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = Path.Combine(outputPath, "cdk.out");
        }

        Directory.CreateDirectory(outputPath);
        return outputPath;
    }
}
