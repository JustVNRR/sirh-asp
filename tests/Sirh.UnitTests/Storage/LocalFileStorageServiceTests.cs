using System.Text;
using Microsoft.Extensions.Options;
using Shouldly;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Infrastructure.Security;
using Sirh.BuildingBlocks.Infrastructure.Storage;
using Xunit;

namespace Sirh.UnitTests.Storage;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"sirh-storage-test-{Guid.NewGuid():N}");
        var storageOptions = Options.Create(new FileStorageOptions
        {
            Provider = "Local",
            LocalRootPath = _tempRoot,
            MaxFileSizeBytes = 1024 * 1024
        });
        var encryptionOptions = Options.Create(new EncryptionOptions
        {
            MasterKey = "LocalFileStorageUnitTestMasterKey12345!"
        });

        var crypto = new AesGcmCryptoService(encryptionOptions);
        var validator = new MagicBytesFileValidator(storageOptions);
        _sut = new LocalFileStorageService(storageOptions, crypto, validator);
    }

    [Fact]
    public async Task SaveAsync_EmployeeSensitive_Should_Encrypt_On_Disk_And_Decrypt_On_Read()
    {
        var pdfContent = Encoding.UTF8.GetBytes("%PDF-1.7\nConfidential Contract Body");
        await using var input = new MemoryStream(pdfContent);

        var stored = await _sut.SaveAsync(
            StorageScope.EmployeeSensitive,
            "alice-durand",
            "contrat.pdf",
            "application/pdf",
            input);

        stored.FileKey.ShouldStartWith("alice-durand/");
        stored.FileName.ShouldBe("contrat.pdf");
        stored.SizeBytes.ShouldBe(pdfContent.LongLength);

        var diskPath = Path.Combine(_tempRoot, "employees-documents", stored.FileKey.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(diskPath).ShouldBeTrue();
        var bytesOnDisk = await File.ReadAllBytesAsync(diskPath);
        bytesOnDisk.ShouldNotBe(pdfContent);

        await using var decryptedStream = await _sut.OpenReadAsync(StorageScope.EmployeeSensitive, stored.FileKey);
        using var ms = new MemoryStream();
        await decryptedStream.CopyToAsync(ms);
        ms.ToArray().ShouldBe(pdfContent);

        var deleted = await _sut.DeleteAsync(StorageScope.EmployeeSensitive, stored.FileKey);
        deleted.ShouldBeTrue();
        File.Exists(diskPath).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_CommonPublic_Should_Store_Plaintext()
    {
        var pdfContent = Encoding.UTF8.GetBytes("%PDF-1.7\nPublic Welcome Booklet");
        await using var input = new MemoryStream(pdfContent);

        var stored = await _sut.SaveAsync(
            StorageScope.CommonPublic,
            "nova-conseil",
            "livret.pdf",
            "application/pdf",
            input);

        var diskPath = Path.Combine(_tempRoot, "common-documents", stored.FileKey.Replace('/', Path.DirectorySeparatorChar));
        var bytesOnDisk = await File.ReadAllBytesAsync(diskPath);
        bytesOnDisk.ShouldBe(pdfContent);
    }

    [Fact]
    public async Task OpenReadAsync_And_SaveAsync_Should_Block_Path_Traversal()
    {
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            _sut.OpenReadAsync(StorageScope.EmployeeSensitive, "../../appsettings.json"));

        await using var pdf = new MemoryStream("%PDF-1.7\nTest"u8.ToArray());
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            _sut.SaveAsync(StorageScope.EmployeeSensitive, "../escape", "test.pdf", "application/pdf", pdf));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
