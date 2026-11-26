namespace Sirh.BuildingBlocks.Application.Storage;

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(
        StorageScope scope,
        string subFolder,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        StorageScope scope,
        string fileKey,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        StorageScope scope,
        string fileKey,
        CancellationToken cancellationToken = default);
}
