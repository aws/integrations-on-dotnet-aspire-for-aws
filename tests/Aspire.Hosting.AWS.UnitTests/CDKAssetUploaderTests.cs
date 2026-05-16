// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.IO.Compression;
using Amazon;
using Amazon.CDK.CloudAssemblySchema;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Aspire.Hosting.AWS.Provisioning;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class CDKAssetUploaderTests
{
    [Theory]
    [InlineData("cdk-hnb659fds-assets-${AWS::AccountId}-${AWS::Region}", "123456789012", "us-west-2", "cdk-hnb659fds-assets-123456789012-us-west-2")]
    [InlineData("my-bucket-${AWS::AccountId}", "000000000000", "eu-west-1", "my-bucket-000000000000")]
    [InlineData("no-tokens-here", "123456789012", "us-east-1", "no-tokens-here")]
    [InlineData("${AWS::Region}-bucket-${AWS::AccountId}", "111111111111", "ap-southeast-2", "ap-southeast-2-bucket-111111111111")]
    public void ResolveTokens_SubstitutesAccountAndRegion(string template, string accountId, string region, string expected)
    {
        var result = CDKAssetUploader.ResolveTokens(template, accountId, region);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ZipDirectory_CreatesZipWithCorrectEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "index.py"), "def handler(event, context): return 'hello'");
            var subDir = Path.Combine(tempDir, "utils");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "helper.py"), "def help(): pass");

            using var stream = new MemoryStream();
            CDKAssetUploader.ZipDirectory(tempDir, stream);
            stream.Position = 0;

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entryNames = archive.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();

            Assert.Contains("index.py", entryNames);
            Assert.Contains("utils/helper.py", entryNames);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task UploadFileAssetsAsync_SkipsExistingS3Objects()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "lambda code");
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new GetObjectMetadataResponse());

            var mockSts = BuildMockSts("123456789012", "us-east-1");
            var uploader = new CDKAssetUploader(NullLogger.Instance, () => mockS3.Object, () => mockSts.Object);

            var assets = BuildFileAssets(Path.GetFileName(tempFile), FileAssetPackaging.FILE,
                "cdk-assets-${AWS::AccountId}-${AWS::Region}", "abc123.zip");

            await uploader.UploadFileAssetsAsync(assets, tempDir, CancellationToken.None);

            mockS3.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task UploadFileAssetsAsync_UploadsFileWhenNotInS3()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "lambda code");
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = System.Net.HttpStatusCode.NotFound });
            mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PutObjectResponse());

            var mockSts = BuildMockSts("123456789012", "us-east-1");
            var uploader = new CDKAssetUploader(NullLogger.Instance, () => mockS3.Object, () => mockSts.Object);

            var assets = BuildFileAssets(Path.GetFileName(tempFile), FileAssetPackaging.FILE,
                "cdk-assets-${AWS::AccountId}-${AWS::Region}", "abc123.bin");

            await uploader.UploadFileAssetsAsync(assets, tempDir, CancellationToken.None);

            mockS3.Verify(s => s.PutObjectAsync(
                It.Is<PutObjectRequest>(r =>
                    r.BucketName == "cdk-assets-123456789012-us-east-1" &&
                    r.Key == "abc123.bin"),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task UploadFileAssetsAsync_ZipsAndUploadsDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var assetDirName = "function";
        var assetDir = Path.Combine(tempDir, assetDirName);
        Directory.CreateDirectory(assetDir);
        File.WriteAllText(Path.Combine(assetDir, "index.py"), "def handler(event, context): return 'hi'");

        try
        {
            string? capturedBucket = null;
            string? capturedKey = null;
            byte[]? capturedZipBytes = null;

            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = System.Net.HttpStatusCode.NotFound });
            mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<PutObjectRequest, CancellationToken>((req, _) =>
                  {
                      capturedBucket = req.BucketName;
                      capturedKey = req.Key;
                      // Read the stream content before it gets disposed
                      using var copy = new MemoryStream();
                      req.InputStream.CopyTo(copy);
                      capturedZipBytes = copy.ToArray();
                  })
                  .ReturnsAsync(new PutObjectResponse());

            var mockSts = BuildMockSts("123456789012", "us-west-2");
            var uploader = new CDKAssetUploader(NullLogger.Instance, () => mockS3.Object, () => mockSts.Object);

            var assets = BuildFileAssets(assetDirName, FileAssetPackaging.ZIP_DIRECTORY,
                "cdk-assets-${AWS::AccountId}-${AWS::Region}", "deadbeef.zip");

            await uploader.UploadFileAssetsAsync(assets, tempDir, CancellationToken.None);

            Assert.Equal("cdk-assets-123456789012-us-west-2", capturedBucket);
            Assert.Equal("deadbeef.zip", capturedKey);
            Assert.NotNull(capturedZipBytes);

            using var archive = new ZipArchive(new MemoryStream(capturedZipBytes), ZipArchiveMode.Read);
            Assert.Single(archive.Entries);
            Assert.Equal("index.py", archive.Entries[0].FullName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task UploadFileAssetsAsync_SkipsExecutableAssets()
    {
        var mockS3 = new Mock<IAmazonS3>();
        var mockSts = BuildMockSts("123456789012", "us-east-1");
        var uploader = new CDKAssetUploader(NullLogger.Instance, () => mockS3.Object, () => mockSts.Object);

        var assets = BuildExecutableFileAssets("cdk-bucket", "exec-asset.zip");

        await uploader.UploadFileAssetsAsync(assets, Path.GetTempPath(), CancellationToken.None);

        mockS3.Verify(s => s.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mockS3.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadFileAssetsAsync_Treats403AsNotFound_AndUploads()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "lambda code");
        var tempDir = Path.GetDirectoryName(tempFile)!;

        try
        {
            var mockS3 = new Mock<IAmazonS3>();
            // Bootstrap bucket policy may return 403 when caller lacks s3:ListBucket
            mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new AmazonS3Exception("Forbidden") { StatusCode = System.Net.HttpStatusCode.Forbidden });
            mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PutObjectResponse());

            var mockSts = BuildMockSts("123456789012", "us-east-1");
            var uploader = new CDKAssetUploader(NullLogger.Instance, () => mockS3.Object, () => mockSts.Object);

            var assets = BuildFileAssets(Path.GetFileName(tempFile), FileAssetPackaging.FILE,
                "cdk-assets-${AWS::AccountId}-${AWS::Region}", "abc123.bin");

            await uploader.UploadFileAssetsAsync(assets, tempDir, CancellationToken.None);

            mockS3.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task UploadFileAssetsAsync_ThrowsWhenNoRegionConfigured()
    {
        var mockS3 = new Mock<IAmazonS3>();

        // IClientConfig.RegionEndpoint is an interface member, so it can be mocked to return
        // null regardless of environment variables — unlike the concrete ClientConfig getter
        // which falls back to the SDK's region resolution chain.
        var mockConfig = new Mock<Amazon.Runtime.IClientConfig>();
        mockConfig.Setup(c => c.RegionEndpoint).Returns((Amazon.RegionEndpoint?)null!);

        var mockSts = new Mock<IAmazonSecurityTokenService>();
        mockSts.Setup(s => s.Config).Returns(mockConfig.Object);
        mockSts.Setup(s => s.GetCallerIdentityAsync(It.IsAny<GetCallerIdentityRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new GetCallerIdentityResponse { Account = "123456789012" });

        var uploader = new CDKAssetUploader(NullLogger.Instance, () => mockS3.Object, () => mockSts.Object);
        var assets = BuildFileAssets("file.zip", FileAssetPackaging.FILE, "my-bucket", "key.zip");

        var ex = await Assert.ThrowsAsync<AWSProvisioningException>(
            () => uploader.UploadFileAssetsAsync(assets, Path.GetTempPath(), CancellationToken.None));

        Assert.Contains("region", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Mock<IAmazonSecurityTokenService> BuildMockSts(string accountId, string region)
    {
        var mockSts = new Mock<IAmazonSecurityTokenService>();
        var config = new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
        mockSts.Setup(s => s.Config).Returns(config);
        mockSts.Setup(s => s.GetCallerIdentityAsync(It.IsAny<GetCallerIdentityRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new GetCallerIdentityResponse { Account = accountId });
        return mockSts;
    }

    private static Dictionary<string, IFileAsset> BuildFileAssets(string sourcePath, FileAssetPackaging packaging, string bucketName, string objectKey)
    {
        var fileSource = new Mock<IFileSource>();
        fileSource.Setup(s => s.Path).Returns(sourcePath);
        fileSource.Setup(s => s.Packaging).Returns(packaging);
        fileSource.Setup(s => s.Executable).Returns((string[]?)null);

        var destination = new Mock<IFileDestination>();
        destination.Setup(d => d.BucketName).Returns(bucketName);
        destination.Setup(d => d.ObjectKey).Returns(objectKey);

        var fileAsset = new Mock<IFileAsset>();
        fileAsset.Setup(a => a.Source).Returns(fileSource.Object);
        fileAsset.Setup(a => a.Destinations).Returns(new Dictionary<string, IFileDestination>
        {
            ["current_account-current_region"] = destination.Object
        });

        return new Dictionary<string, IFileAsset> { ["asset-hash"] = fileAsset.Object };
    }

    private static Dictionary<string, IFileAsset> BuildExecutableFileAssets(string bucketName, string objectKey)
    {
        var fileSource = new Mock<IFileSource>();
        fileSource.Setup(s => s.Executable).Returns(new[] { "node", "bundle.js" });
        fileSource.Setup(s => s.Path).Returns((string?)null);

        var destination = new Mock<IFileDestination>();
        destination.Setup(d => d.BucketName).Returns(bucketName);
        destination.Setup(d => d.ObjectKey).Returns(objectKey);

        var fileAsset = new Mock<IFileAsset>();
        fileAsset.Setup(a => a.Source).Returns(fileSource.Object);
        fileAsset.Setup(a => a.Destinations).Returns(new Dictionary<string, IFileDestination>
        {
            ["current_account-current_region"] = destination.Object
        });

        return new Dictionary<string, IFileAsset> { ["exec-asset"] = fileAsset.Object };
    }
}
