using Sirh.BuildingBlocks.Domain.Results;

namespace Sirh.Modules.HumanResources.Application.Services;

internal static class HrConcurrencyHelper
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

        if (currentRowVersion.Length > 0 && !buffer[..bytesWritten].SequenceEqual(currentRowVersion))
        {
            return Result.Failure(Error.Conflict(
                "Concurrency.Conflict",
                "The record was modified concurrently. Please refresh and retry."));
        }

        return Result.Success();
    }
}
