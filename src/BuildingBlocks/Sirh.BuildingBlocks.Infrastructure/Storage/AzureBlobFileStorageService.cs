using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Storage;

namespace Sirh.BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IFileStorageService"/> compatible with Azurite and Azure Storage.
/// Sensitive employee files are encrypted with AES-256-GCM before leaving the application boundary.
/// </summary>
public sealed class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly FileStorageOptions _options;
    private readonly IDataProtectorService _cryptoService;
    private readonly IFileValidator _fileValidator;
    private readonly ConcurrentDictionary<string, byte> _ensuredContainers = new(StringComparer.OrdinalIgnoreCase);

    public AzureBlobFileStorageService(
        BlobServiceClient blobServiceClient,
        IOptions<FileStorageOptions> options,
        IDataProtectorService cryptoService,
        IFileValidator fileValidator)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cryptoService);
        ArgumentNullException.ThrowIfNull(fileValidator);

        _blobServiceClient = blobServiceClient;
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

            workingStream.Position = 0;
            using var rawBuffer = new MemoryStream();
            await workingStream.CopyToAsync(rawBuffer, cancellationToken);
            var rawBytes = rawBuffer.ToArray();

            // Encrypt sensitive files before transmitting them to Azure Blob Storage / Azurite.
            var bytesToUpload = scope == StorageScope.EmployeeSensitive
                ? _cryptoService.EncryptBytes(rawBytes)
                : rawBytes;

            var effectiveContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;

            var containerClient = await GetContainerClientAsync(scope, ensureExists: true, cancellationToken);
            var blobClient = containerClient.GetBlobClient(fileKey);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = scope == StorageScope.EmployeeSensitive
                        ? "application/octet-stream"
                        : effectiveContentType
                }
            };

            await blobClient.UploadAsync(BinaryData.FromBytes(bytesToUpload), uploadOptions, cancellationToken);

            return new StoredFile(
                fileKey,
                safeOriginalName,
                effectiveContentType,
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
        var normalizedKey = ValidateBlobKey(fileKey);
        var containerClient = await GetContainerClientAsync(scope, ensureExists: false, cancellationToken);
        var blobClient = containerClient.GetBlobClient(normalizedKey);

        try
        {
            var download = await blobClient.DownloadContentAsync(cancellationToken);
            var payload = download.Value.Content.ToArray();

            var resultBytes = scope == StorageScope.EmployeeSensitive
                ? _cryptoService.DecryptBytes(payload)
                : payload;

            return new MemoryStream(resultBytes, writable: false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"Stored file not found for key '{fileKey}'.", fileKey, ex);
        }
    }

    public async Task<bool> DeleteAsync(
        StorageScope scope,
        string fileKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = ValidateBlobKey(fileKey);
        var containerClient = await GetContainerClientAsync(scope, ensureExists: false, cancellationToken);
        var blobClient = containerClient.GetBlobClient(normalizedKey);

        var response = await blobClient.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken);

        return response.Value;
    }

    private async Task<BlobContainerClient> GetContainerClientAsync(
        StorageScope scope,
        bool ensureExists,
        CancellationToken cancellationToken)
    {
        var containerName = scope == StorageScope.EmployeeSensitive
            ? _options.EmployeeSensitiveContainer
            : _options.CommonPublicContainer;

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException($"Blob container name is not configured for scope '{scope}'.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        if (ensureExists && _options.CreateContainersIfMissing && !_ensuredContainers.ContainsKey(containerName))
        {
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            _ensuredContainers.TryAdd(containerName, 0);
        }

        return containerClient;
    }

    private static string ValidateBlobKey(string fileKey)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
        {
            throw new UnauthorizedAccessException("File key cannot be empty.");
        }

        var normalized = fileKey.Replace('\\', '/').TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(static s => s is "." or ".."))
        {
            throw new UnauthorizedAccessException($"Path traversal detected for key '{fileKey}'.");
        }

        return string.Join('/', segments);
    }

    private static string SanitizeSubFolder(string subFolder)
    {
        if (string.IsNullOrWhiteSpace(subFolder))
        {
            return string.Empty;
        }

        var segments = subFolder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new UnauthorizedAccessException($"Invalid storage subfolder '{subFolder}'.");
            }
        }

        return string.Join('/', segments);
    }
}
