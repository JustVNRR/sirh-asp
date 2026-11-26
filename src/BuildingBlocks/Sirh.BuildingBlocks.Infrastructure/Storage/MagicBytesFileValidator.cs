using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Storage;

namespace Sirh.BuildingBlocks.Infrastructure.Storage;

public sealed class MagicBytesFileValidator : IFileValidator
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffMagic = "RIFF"u8.ToArray();
    private static readonly byte[] WebpMagic = "WEBP"u8.ToArray();

    private readonly FileStorageOptions _options;

    public MagicBytesFileValidator(IOptions<FileStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task<FileValidationResult> ValidateAsync(
        Stream content,
        string fileName,
        string declaredContentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return FileValidationResult.Invalid("File name is required.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) ||
            !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return FileValidationResult.Invalid($"File extension '{extension}' is not allowed.");
        }

        if (!content.CanSeek)
        {
            return FileValidationResult.Invalid("File stream must be seekable for validation.");
        }

        if (content.Length <= 0)
        {
            return FileValidationResult.Invalid("File cannot be empty.");
        }

        if (content.Length > _options.MaxFileSizeBytes)
        {
            return FileValidationResult.Invalid(
                $"File exceeds maximum allowed size of {_options.MaxFileSizeBytes} bytes.");
        }

        var header = new byte[Math.Min(16, (int)content.Length)];
        content.Position = 0;
        var bytesRead = await content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        content.Position = 0;

        if (!MatchesMagicBytes(extension, header.AsSpan(0, bytesRead)))
        {
            return FileValidationResult.Invalid(
                $"File content does not match the declared extension '{extension}'.");
        }

        return FileValidationResult.Valid();
    }

    private static bool MatchesMagicBytes(string extension, ReadOnlySpan<byte> header) => extension switch
    {
        ".pdf" => header.Length >= PdfMagic.Length && header[..PdfMagic.Length].SequenceEqual(PdfMagic),
        ".png" => header.Length >= PngMagic.Length && header[..PngMagic.Length].SequenceEqual(PngMagic),
        ".jpg" or ".jpeg" => header.Length >= JpegMagic.Length && header[..JpegMagic.Length].SequenceEqual(JpegMagic),
        ".webp" => header.Length >= 12
                   && header[..4].SequenceEqual(RiffMagic)
                   && header.Slice(8, 4).SequenceEqual(WebpMagic),
        _ => false
    };
}
