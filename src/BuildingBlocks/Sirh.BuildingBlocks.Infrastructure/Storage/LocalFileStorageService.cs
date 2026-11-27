using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Storage;

namespace Sirh.BuildingBlocks.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly IDataProtectorService _cryptoService;
    private readonly IFileValidator _fileValidator;

    public LocalFileStorageService(
        IOptions<FileStorageOptions> options,
        IDataProtectorService cryptoService,
        IFileValidator fileValidator)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _cryptoService = cryptoService;
        _fileValidator = fileValidator;
    }

    public async Task<StoredFile> SaveAsync(
        StorageScope scope,
        string subFolder,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        Stream workingStream = content;
        MemoryStream? bufferedStream = null;

        try
        {
            if (!content.CanSeek)
            {
                bufferedStream = new MemoryStream();
                await content.CopyToAsync(bufferedStream, cancellationToken);
                bufferedStream.Position = 0;
                workingStream = bufferedStream;
            }

            var validation = await _fileValidator.ValidateAsync(
                workingStream,
                originalFileName,
                contentType,
                cancellationToken);

            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.ErrorMessage ?? "Invalid file.");
            }

            var safeOriginalName = Path.GetFileName(originalFileName);
            var extension = Path.GetExtension(safeOriginalName).ToLowerInvariant();
            var sanitizedSubFolder = SanitizeSubFolder(subFolder);
            var generatedName = $"{Guid.CreateVersion7():N}{extension}";
            var fileKey = string.IsNullOrEmpty(sanitizedSubFolder)
                ? generatedName
                : $"{sanitizedSubFolder}/{generatedName}";

            var scopeRoot = GetScopeRootPath(scope);
            var fullPath = ResolveSafePath(scopeRoot, fileKey);
            var directory = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(directory);

            workingStream.Position = 0;
            using var rawBuffer = new MemoryStream();
            await workingStream.CopyToAsync(rawBuffer, cancellationToken);
            var rawBytes = rawBuffer.ToArray();

            var bytesToWrite = scope == StorageScope.EmployeeSensitive
                ? _cryptoService.EncryptBytes(rawBytes)
                : rawBytes;

            await File.WriteAllBytesAsync(fullPath, bytesToWrite, cancellationToken);

            return new StoredFile(
                fileKey,
                safeOriginalName,
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                rawBytes.LongLength);
        }
        finally
        {
            if (bufferedStream is not null)
            {
                await bufferedStream.DisposeAsync();
            }
        }
    }

    public async Task<Stream> OpenReadAsync(
        StorageScope scope,
        string fileKey,
        CancellationToken cancellationToken = default)
    {
        var scopeRoot = GetScopeRootPath(scope);
        var fullPath = ResolveSafePath(scopeRoot, fileKey);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Stored file not found for key '{fileKey}'.", fileKey);
        }

        if (scope == StorageScope.EmployeeSensitive)
        {
            var encryptedBytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            var plainBytes = _cryptoService.DecryptBytes(encryptedBytes);
            return new MemoryStream(plainBytes, writable: false);
        }

        return new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
    }

    public Task<bool> DeleteAsync(
        StorageScope scope,
        string fileKey,
        CancellationToken cancellationToken = default)
    {
        var scopeRoot = GetScopeRootPath(scope);
        var fullPath = ResolveSafePath(scopeRoot, fileKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    private string GetScopeRootPath(StorageScope scope)
    {
        var scopeFolder = scope == StorageScope.EmployeeSensitive
            ? "employees-documents"
            : "common-documents";

        return Path.GetFullPath(Path.Combine(_options.LocalRootPath, scopeFolder));
    }

    private static string ResolveSafePath(string rootPath, string relativeKey)
    {
        if (string.IsNullOrWhiteSpace(relativeKey))
        {
            throw new UnauthorizedAccessException("File key cannot be empty.");
        }

        var normalizedKey = relativeKey.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var combined = Path.GetFullPath(Path.Combine(rootPath, normalizedKey));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Path traversal detected for key '{relativeKey}'.");
        }

        return combined;
    }

    private static string SanitizeSubFolder(string subFolder)
    {
        if (string.IsNullOrWhiteSpace(subFolder))
        {
            return string.Empty;
        }

        var segments = subFolder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(static segment => segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new UnauthorizedAccessException($"Invalid storage subfolder '{subFolder}'.");
        }

        return string.Join('/', segments);
    }
}
