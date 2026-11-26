namespace Sirh.BuildingBlocks.Application.Storage;

public sealed record FileValidationResult(bool IsValid, string? ErrorMessage = null)
{
    public static FileValidationResult Valid() => new(true);
    public static FileValidationResult Invalid(string message) => new(false, message);
}

public interface IFileValidator
{
    Task<FileValidationResult> ValidateAsync(
        Stream content,
        string fileName,
        string declaredContentType,
        CancellationToken cancellationToken = default);
}
