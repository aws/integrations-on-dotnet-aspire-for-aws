// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.IO.Compression;
using Amazon.CDK.CloudAssemblySchema;
using Amazon.CDK.CXAPI;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Provisioning;

internal sealed class CDKAssetUploader
{
    private readonly IAWSSDKConfig? _awsSdkConfig;
    private readonly ILogger _logger;
    private readonly Func<IAmazonS3>? _s3ClientFactory;
    private readonly Func<IAmazonSecurityTokenService>? _stsClientFactory;

    public CDKAssetUploader(IAWSSDKConfig? awsSdkConfig, ILogger logger)
    {
        _awsSdkConfig = awsSdkConfig;
        _logger = logger;
    }

    internal CDKAssetUploader(ILogger logger, Func<IAmazonS3> s3ClientFactory, Func<IAmazonSecurityTokenService> stsClientFactory)
    {
        _logger = logger;
        _s3ClientFactory = s3ClientFactory;
        _stsClientFactory = stsClientFactory;
    }

    public async Task UploadAssetsAsync(AssetManifestArtifact artifact, CancellationToken cancellationToken)
    {
        var fileAssets = artifact.Contents.Files;
        if (fileAssets == null || fileAssets.Count == 0)
        {
            return;
        }

        await UploadFileAssetsAsync(fileAssets, artifact.Assembly.Directory, cancellationToken).ConfigureAwait(false);
    }

    internal async Task UploadFileAssetsAsync(IDictionary<string, IFileAsset> fileAssets, string assemblyDirectory, CancellationToken cancellationToken)
    {
        var (accountId, region) = await GetCallerAccountAndRegionAsync(cancellationToken).ConfigureAwait(false);
        using var s3Client = CreateS3Client();

        foreach (var (_, fileAsset) in fileAssets)
        {
            await UploadFileAssetAsync(s3Client, fileAsset, assemblyDirectory, accountId, region, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UploadFileAssetAsync(IAmazonS3 s3Client, IFileAsset fileAsset, string assemblyDirectory, string accountId, string region, CancellationToken cancellationToken)
    {
        var source = fileAsset.Source;

        if (source.Executable is { Length: > 0 })
        {
            _logger.LogWarning("Skipping CDK asset with executable source; executable-generated assets are not supported");
            return;
        }

        if (source.Path == null)
        {
            return;
        }

        var sourcePath = Path.Combine(assemblyDirectory, source.Path);
        var packaging = source.Packaging ?? FileAssetPackaging.FILE;

        foreach (var (_, destination) in fileAsset.Destinations)
        {
            var bucketName = ResolveTokens(destination.BucketName, accountId, region);
            var objectKey = destination.ObjectKey;

            if (await AssetExistsInS3Async(s3Client, bucketName, objectKey, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogDebug("CDK asset {ObjectKey} already exists in {Bucket}, skipping upload", objectKey, bucketName);
                continue;
            }

            _logger.LogInformation("Uploading CDK asset {ObjectKey} to s3://{Bucket}", objectKey, bucketName);

            if (packaging == FileAssetPackaging.ZIP_DIRECTORY)
            {
                await UploadZippedDirectoryAsync(s3Client, sourcePath, bucketName, objectKey, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await UploadFileAsync(s3Client, sourcePath, bucketName, objectKey, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Successfully uploaded CDK asset {ObjectKey} to s3://{Bucket}", objectKey, bucketName);
        }
    }

    private static async Task<bool> AssetExistsInS3Async(IAmazonS3 s3Client, string bucketName, string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(bucketName, objectKey, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static async Task UploadFileAsync(IAmazonS3 s3Client, string filePath, string bucketName, string objectKey, CancellationToken cancellationToken)
    {
        using var fileStream = File.OpenRead(filePath);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = fileStream,
        };
        await s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task UploadZippedDirectoryAsync(IAmazonS3 s3Client, string directoryPath, string bucketName, string objectKey, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        ZipDirectory(directoryPath, memoryStream);
        memoryStream.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = memoryStream,
        };
        await s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal static void ZipDirectory(string directoryPath, Stream outputStream)
    {
        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var file in new DirectoryInfo(directoryPath).GetFiles("*", SearchOption.AllDirectories))
        {
            var entryName = Path.GetRelativePath(directoryPath, file.FullName).Replace('\\', '/');
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var fileStream = file.OpenRead();
            fileStream.CopyTo(entryStream);
        }
    }

    internal static string ResolveTokens(string value, string accountId, string region) =>
        value
            .Replace("${AWS::AccountId}", accountId, StringComparison.Ordinal)
            .Replace("${AWS::Region}", region, StringComparison.Ordinal);

    private async Task<(string accountId, string region)> GetCallerAccountAndRegionAsync(CancellationToken cancellationToken)
    {
        using var stsClient = CreateStsClient();
        var response = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest(), cancellationToken).ConfigureAwait(false);
        var region = stsClient.Config.RegionEndpoint?.SystemName ?? "us-east-1";
        return (response.Account, region);
    }

    private IAmazonS3 CreateS3Client()
    {
        if (_s3ClientFactory != null)
        {
            return _s3ClientFactory();
        }

        AmazonS3Client client;
        if (_awsSdkConfig != null)
        {
            var config = _awsSdkConfig.CreateServiceConfig<AmazonS3Config>();
            var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
            client = new AmazonS3Client(credentials, config);
        }
        else
        {
            client = new AmazonS3Client();
        }
        client.BeforeRequestEvent += SdkUtilities.ConfigureUserAgentString;
        return client;
    }

    private IAmazonSecurityTokenService CreateStsClient()
    {
        if (_stsClientFactory != null)
        {
            return _stsClientFactory();
        }

        AmazonSecurityTokenServiceClient client;
        if (_awsSdkConfig != null)
        {
            var config = _awsSdkConfig.CreateServiceConfig<AmazonSecurityTokenServiceConfig>();
            var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
            client = new AmazonSecurityTokenServiceClient(credentials, config);
        }
        else
        {
            client = new AmazonSecurityTokenServiceClient();
        }
        client.BeforeRequestEvent += SdkUtilities.ConfigureUserAgentString;
        return client;
    }
}
