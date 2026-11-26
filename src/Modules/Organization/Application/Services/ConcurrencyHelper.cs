using Sirh.BuildingBlocks.Domain.Results;
using Sirh.BuildingBlocks.Domain.Text;

namespace Sirh.Modules.Organization.Application.Services;

internal static class ConcurrencyHelper
{
    public static Result ValidateRowVersion(string? rowVersionBase64, byte[] currentRowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersionBase64))
        {
            return Result.Success();
        }

        var trimmed = rowVersionBase64.Trim();
        Span<byte> buffer = stackalloc byte[trimmed.Length];
        if (!Convert.TryFromBase64String(trimmed, buffer, out var bytesWritten))
        {
            return Result.Failure(Error.Validation(
                "Concurrency.InvalidRowVersion",
                "RowVersion must be a valid Base64 string."));
        }

        var provided = buffer[..bytesWritten];
        if (currentRowVersion.Length > 0 && !provided.SequenceEqual(currentRowVersion))
        {
            return Result.Failure(Error.Conflict(
                "Concurrency.Conflict",
                "The resource was modified concurrently. Please refresh and retry."));
        }

        return Result.Success();
    }

    public static async Task<string> GenerateUniqueSlugAsync(
        string sourceText,
        Func<string, Task<bool>> slugExistsAsync,
        string fallbackPrefix = "item")
    {
        var baseSlug = SlugGenerator.Generate(sourceText, maxLength: 90);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = fallbackPrefix;
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await slugExistsAsync(candidate))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
