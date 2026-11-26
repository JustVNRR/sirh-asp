using Microsoft.Extensions.Options;
using Shouldly;
using Sirh.BuildingBlocks.Infrastructure.Storage;
using Xunit;

namespace Sirh.UnitTests.Storage;

public sealed class MagicBytesFileValidatorTests
{
    private readonly MagicBytesFileValidator _sut = new(
        Options.Create(new FileStorageOptions
        {
            MaxFileSizeBytes = 1024,
            AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".webp"]
        }));

    [Theory]
    [InlineData("contract.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 })]
    [InlineData("scan.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 })]
    [InlineData("photo.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })]
    [InlineData("avatar.webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 })]
    public async Task ValidateAsync_Should_Accept_Valid_MagicBytes_And_Reset_Position(
        string fileName,
        byte[] content)
    {
        await using var stream = new MemoryStream(content);
        stream.Position = 2;

        var result = await _sut.ValidateAsync(stream, fileName, "application/octet-stream");

        result.IsValid.ShouldBeTrue();
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_Should_Reject_Executable_Renamed_As_Pdf()
    {
        await using var stream = new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00]);

        var result = await _sut.ValidateAsync(stream, "malware.pdf", "application/pdf");

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("does not match");
    }

    [Fact]
    public async Task ValidateAsync_Should_Reject_Disallowed_Extension()
    {
        await using var stream = new MemoryStream("PK\x03\x04dummy"u8.ToArray());

        var result = await _sut.ValidateAsync(stream, "legacy.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_Should_Reject_Empty_And_Oversized_Files()
    {
        await using var empty = new MemoryStream();
        var emptyResult = await _sut.ValidateAsync(empty, "empty.pdf", "application/pdf");
        emptyResult.IsValid.ShouldBeFalse();

        var oversizedBytes = new byte[2048];
        "%PDF-1.7"u8.CopyTo(oversizedBytes);
        await using var oversized = new MemoryStream(oversizedBytes);
        var oversizedResult = await _sut.ValidateAsync(oversized, "big.pdf", "application/pdf");
        oversizedResult.IsValid.ShouldBeFalse();
    }
}
