using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Infrastructure;
using Sirh.BuildingBlocks.Infrastructure.Health;
using Sirh.BuildingBlocks.Infrastructure.Security;
using Sirh.BuildingBlocks.Infrastructure.Storage;
using Xunit;

namespace Sirh.UnitTests.Storage;

public sealed class AzureBlobFileStorageServiceTests
{
    private static readonly byte[] SamplePdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.7\n% Confidential SIRH document content\n%%EOF");

    private static readonly IOptions<FileStorageOptions> DefaultOptions = Options.Create(new FileStorageOptions
    {
        Provider = FileStorageOptions.AzureBlobProvider,
        AzureConnectionString = "UseDevelopmentStorage=true",
        EmployeeSensitiveContainer = "employees-documents",
        CommonPublicContainer = "common-documents",
        CreateContainersIfMissing = true
    });

    private static AesGcmCryptoService CreateCryptoService() => new(Options.Create(new EncryptionOptions
    {
        MasterKey = "UnitTestMasterKeyWithAtLeast32Chars!!"
    }));

    [Fact]
    public async Task SaveAndOpenRead_EmployeeSensitive_Should_Encrypt_Before_Upload_And_Use_Dedicated_Container()
    {
        var blobServiceClient = Substitute.For<BlobServiceClient>();
        var containerClient = Substitute.For<BlobContainerClient>();
        var blobClient = Substitute.For<BlobClient>();

        blobServiceClient.GetBlobContainerClient("employees-documents").Returns(containerClient);
        containerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);

        byte[]? uploadedBytes = null;
        BlobUploadOptions? capturedOptions = null;

        blobClient.UploadAsync(
                Arg.Do<BinaryData>(data => uploadedBytes = data.ToArray()),
                Arg.Do<BlobUploadOptions>(opts => capturedOptions = opts),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Substitute.For<Response<BlobContentInfo>>()));

        blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobDownloadResult(content: BinaryData.FromBytes(uploadedBytes!)),
                Substitute.For<Response>())));

        var service = new AzureBlobFileStorageService(
            blobServiceClient,
            DefaultOptions,
            CreateCryptoService(),
            new MagicBytesFileValidator(DefaultOptions));

        await using var inputStream = new MemoryStream(SamplePdfBytes);
        var stored = await service.SaveAsync(
            StorageScope.EmployeeSensitive,
            "emp-42/contracts",
            "contract.pdf",
            "application/pdf",
            inputStream);

        stored.FileKey.ShouldStartWith("emp-42/contracts/");
        stored.FileKey.ShouldEndWith(".pdf");
        stored.FileName.ShouldBe("contract.pdf");
        stored.ContentType.ShouldBe("application/pdf");
        stored.SizeBytes.ShouldBe(SamplePdfBytes.LongLength);

        await containerClient.Received(1).CreateIfNotExistsAsync(
            PublicAccessType.None,
            Arg.Any<IDictionary<string, string>?>(),
            Arg.Any<BlobContainerEncryptionScopeOptions?>(),
            Arg.Any<CancellationToken>());

        // Uploaded blob must already be encrypted with AES-256-GCM (not raw PDF bytes)
        uploadedBytes.ShouldNotBeNull();
        uploadedBytes.ShouldNotBe(SamplePdfBytes);
        Encoding.ASCII.GetString(uploadedBytes).ShouldNotContain("%PDF-");
        capturedOptions.ShouldNotBeNull();
        capturedOptions.HttpHeaders.ContentType.ShouldBe("application/octet-stream");

        await using var readStream = await service.OpenReadAsync(StorageScope.EmployeeSensitive, stored.FileKey);
        using var output = new MemoryStream();
        await readStream.CopyToAsync(output);
        output.ToArray().ShouldBe(SamplePdfBytes);
    }

    [Fact]
    public async Task SaveAndOpenRead_CommonPublic_Should_Store_Plaintext_In_Common_Container()
    {
        var blobServiceClient = Substitute.For<BlobServiceClient>();
        var containerClient = Substitute.For<BlobContainerClient>();
        var blobClient = Substitute.For<BlobClient>();

        blobServiceClient.GetBlobContainerClient("common-documents").Returns(containerClient);
        containerClient.GetBlobClient(Arg.Any<string>()).Returns(blobClient);

        byte[]? uploadedBytes = null;
        BlobUploadOptions? capturedOptions = null;

        blobClient.UploadAsync(
                Arg.Do<BinaryData>(data => uploadedBytes = data.ToArray()),
                Arg.Do<BlobUploadOptions>(opts => capturedOptions = opts),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Substitute.For<Response<BlobContentInfo>>()));

        blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobDownloadResult(content: BinaryData.FromBytes(uploadedBytes!)),
                Substitute.For<Response>())));

        var service = new AzureBlobFileStorageService(
            blobServiceClient,
            DefaultOptions,
            CreateCryptoService(),
            new MagicBytesFileValidator(DefaultOptions));

        await using var inputStream = new MemoryStream(SamplePdfBytes);
        var stored = await service.SaveAsync(
            StorageScope.CommonPublic,
            "welcome-kits",
            "guide.pdf",
            "application/pdf",
            inputStream);

        stored.FileKey.ShouldStartWith("welcome-kits/");
        uploadedBytes.ShouldBe(SamplePdfBytes);
        capturedOptions!.HttpHeaders.ContentType.ShouldBe("application/pdf");

        await using var readStream = await service.OpenReadAsync(StorageScope.CommonPublic, stored.FileKey);
        using var output = new MemoryStream();
        await readStream.CopyToAsync(output);
        output.ToArray().ShouldBe(SamplePdfBytes);
    }

    [Fact]
    public async Task DeleteAsync_And_OpenReadAsync_Missing_Should_Behave_As_Contracted()
    {
        var blobServiceClient = Substitute.For<BlobServiceClient>();
        var containerClient = Substitute.For<BlobContainerClient>();
        var blobClient = Substitute.For<BlobClient>();

        blobServiceClient.GetBlobContainerClient("common-documents").Returns(containerClient);
        containerClient.GetBlobClient("kits/missing.pdf").Returns(blobClient);

        blobClient.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                Arg.Any<BlobRequestConditions?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(Response.FromValue(true, Substitute.For<Response>())),
                Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

        blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Response<BlobDownloadResult>>>(_ => throw new RequestFailedException(404, "BlobNotFound"));

        var service = new AzureBlobFileStorageService(
            blobServiceClient,
            DefaultOptions,
            CreateCryptoService(),
            new MagicBytesFileValidator(DefaultOptions));

        (await service.DeleteAsync(StorageScope.CommonPublic, "kits/missing.pdf")).ShouldBeTrue();
        (await service.DeleteAsync(StorageScope.CommonPublic, "kits/missing.pdf")).ShouldBeFalse();

        await Should.ThrowAsync<FileNotFoundException>(() =>
            service.OpenReadAsync(StorageScope.CommonPublic, "kits/missing.pdf"));
    }

    [Fact]
    public async Task Save_And_Read_Should_Reject_Invalid_MagicBytes_And_PathTraversal()
    {
        var blobServiceClient = Substitute.For<BlobServiceClient>();
        var service = new AzureBlobFileStorageService(
            blobServiceClient,
            DefaultOptions,
            CreateCryptoService(),
            new MagicBytesFileValidator(DefaultOptions));

        await using var fakePdf = new MemoryStream(Encoding.ASCII.GetBytes("MZ-not-a-pdf"));
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.SaveAsync(StorageScope.CommonPublic, "kits", "fake.pdf", "application/pdf", fakePdf));

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.OpenReadAsync(StorageScope.CommonPublic, "../secrets/key.pdf"));
    }

    [Fact]
    public void DependencyInjection_Should_Switch_Between_Local_And_AzureBlob_By_Configuration()
    {
        var azureConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:MasterKey"] = "UnitTestMasterKeyWithAtLeast32Chars!!",
                ["Storage:Provider"] = "AzureBlob",
                ["Storage:AzureConnectionString"] = "UseDevelopmentStorage=true"
            })
            .Build();

        var azureServices = new ServiceCollection();
        azureServices.AddBuildingBlocksInfrastructure(azureConfig);
        using var azureProvider = azureServices.BuildServiceProvider();
        azureProvider.GetRequiredService<IFileStorageService>().ShouldBeOfType<AzureBlobFileStorageService>();

        var localConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:MasterKey"] = "UnitTestMasterKeyWithAtLeast32Chars!!",
                ["Storage:Provider"] = "Local"
            })
            .Build();

        var localServices = new ServiceCollection();
        localServices.AddBuildingBlocksInfrastructure(localConfig);
        using var localProvider = localServices.BuildServiceProvider();
        localProvider.GetRequiredService<IFileStorageService>().ShouldBeOfType<LocalFileStorageService>();

        var unknownConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:MasterKey"] = "UnitTestMasterKeyWithAtLeast32Chars!!",
                ["Storage:Provider"] = "UnknownCloud"
            })
            .Build();

        Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddBuildingBlocksInfrastructure(unknownConfig));
    }

    [Fact]
    public async Task FileStorageHealthCheck_Should_Report_AzureBlob_Status()
    {
        var blobServiceClient = Substitute.For<BlobServiceClient>();
        blobServiceClient.GetAccountInfoAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(Substitute.For<Response<AccountInfo>>()),
                Task.FromException<Response<AccountInfo>>(new RequestFailedException(503, "Unavailable")));

        var sp = new ServiceCollection()
            .AddSingleton(blobServiceClient)
            .BuildServiceProvider();

        var healthCheck = new FileStorageHealthCheck(DefaultOptions, sp);

        var healthy = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        healthy.Status.ShouldBe(HealthStatus.Healthy);

        var unhealthy = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        unhealthy.Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
