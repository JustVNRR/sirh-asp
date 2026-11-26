using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.DTOs;

namespace Sirh.Modules.Contracts.Application.Services;

internal static class PdfUploadValidator
{
    public static async Task<Result> ValidateAsync(
        UploadedPdfInput? file,
        IFileValidator fileValidator,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Result.Failure(Error.Validation(
                "File.Required",
                "A PDF file is required."));
        }

        if (string.IsNullOrWhiteSpace(file.FileName) ||
            !string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation(
                "File.OnlyPdfAllowed",
                "Only PDF files (.pdf) are accepted."));
        }

        if (file.Content.CanSeek)
        {
            file.Content.Position = 0;
        }

        var validation = await fileValidator.ValidateAsync(
            file.Content,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType,
            cancellationToken);

        if (file.Content.CanSeek)
        {
            file.Content.Position = 0;
        }

        if (!validation.IsValid)
        {
            return Result.Failure(Error.Validation(
                "File.InvalidPdf",
                validation.ErrorMessage ?? "The uploaded file is not a valid PDF."));
        }

        return Result.Success();
    }
}
