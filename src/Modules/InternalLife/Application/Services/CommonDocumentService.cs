using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Constants;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.Services;

public sealed class CommonDocumentService(
    ICommonDocumentRepository documentRepository,
    IOrganizationLookup organizationLookup,
    IFileValidator fileValidator,
    IFileStorageService fileStorageService,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser) : ICommonDocumentService
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp"
        };

    public async Task<Result<PagedResult<CommonDocumentDto>>> GetPagedAsync(
        CommonDocumentQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        if (!CanRead())
        {
            return Error.Forbidden(
                "CommonDocuments.Forbidden",
                "You do not have permission to view common documents.");
        }

        var page = await documentRepository.GetPagedAsync(
            query.CompanyId,
            query.Category,
            query.IsWelcomeKit,
            query.ContractTypeId,
            query.Archived,
            query.Search,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(MapToDto).ToArray();
        return PagedResult<CommonDocumentDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<CommonDocumentDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!CanRead())
        {
            return Error.Forbidden(
                "CommonDocuments.Forbidden",
                "You do not have permission to view common documents.");
        }

        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Error.NotFound("CommonDocuments.NotFound", $"Common document '{id}' was not found.");
        }

        return MapToDto(document);
    }

    public async Task<Result<DownloadedCommonFileDto>> OpenFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!CanRead())
        {
            return Error.Forbidden(
                "CommonDocuments.Forbidden",
                "You do not have permission to download common documents.");
        }

        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Error.NotFound("CommonDocuments.NotFound", $"Common document '{id}' was not found.");
        }

        var stream = await fileStorageService.OpenReadAsync(
            StorageScope.CommonPublic,
            document.FileKey,
            cancellationToken);

        return new DownloadedCommonFileDto(
            stream,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes);
    }

    public async Task<Result<CommonDocumentDto>> CreateAsync(
        CreateCommonDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command.Category))
        {
            return Error.Validation("CommonDocuments.InvalidCategory", $"Unsupported category '{command.Category}'.");
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Error.Validation("CommonDocuments.TitleRequired", "Title is required.");
        }

        var effectiveIsWelcomeKit = command.IsWelcomeKit ?? CommonDocumentTaxonomy.IsWelcomeKitCategory(command.Category);
        if (!CanWrite(command.Category, effectiveIsWelcomeKit))
        {
            return Error.Forbidden(
                "CommonDocuments.WriteForbidden",
                "You do not have permission to create common documents in this category.");
        }

        var fileValidation = await ValidateUploadedFileAsync(command.File, cancellationToken);
        if (fileValidation.IsFailure)
        {
            return fileValidation.Error;
        }

        var refsValidation = await ValidateReferencesAsync(command.CompanyId, command.ContractTypeId, cancellationToken);
        if (refsValidation.IsFailure)
        {
            return refsValidation.Error;
        }

        var subFolder = $"common-documents/{command.Category.ToString().ToLowerInvariant()}";
        var stored = await fileStorageService.SaveAsync(
            StorageScope.CommonPublic,
            subFolder,
            command.File.FileName,
            fileValidation.Value,
            command.File.Content,
            cancellationToken);

        var fileDate = command.FileDate ?? dateTimeProvider.TodayUtc;
        var document = new CommonDocument(
            command.CompanyId,
            command.ContractTypeId,
            command.Category,
            effectiveIsWelcomeKit,
            command.Title,
            command.Description,
            stored.FileKey,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            fileDate);

        await documentRepository.AddAsync(document, cancellationToken);
        await documentRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(document);
    }

    public async Task<Result<CommonDocumentDto>> UpdateAsync(
        Guid id,
        UpdateCommonDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command.Category))
        {
            return Error.Validation("CommonDocuments.InvalidCategory", $"Unsupported category '{command.Category}'.");
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Error.Validation("CommonDocuments.TitleRequired", "Title is required.");
        }

        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Error.NotFound("CommonDocuments.NotFound", $"Common document '{id}' was not found.");
        }

        var targetIsWelcomeKit = command.IsWelcomeKit ?? CommonDocumentTaxonomy.IsWelcomeKitCategory(command.Category);
        if (!CanWrite(document.Category, document.IsWelcomeKit) ||
            !CanWrite(command.Category, targetIsWelcomeKit))
        {
            return Error.Forbidden(
                "CommonDocuments.WriteForbidden",
                "You do not have permission to modify this common document or category.");
        }

        var rowVersionResult = ValidateRowVersion(command.RowVersion, document.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        var refsValidation = await ValidateReferencesAsync(command.CompanyId, command.ContractTypeId, cancellationToken);
        if (refsValidation.IsFailure)
        {
            return refsValidation.Error;
        }

        var effectiveFileDate = command.FileDate ?? document.FileDate;
        string? oldFileKey = null;

        if (command.File is not null)
        {
            var fileValidation = await ValidateUploadedFileAsync(command.File, cancellationToken);
            if (fileValidation.IsFailure)
            {
                return fileValidation.Error;
            }

            var stored = await fileStorageService.SaveAsync(
                StorageScope.CommonPublic,
                $"common-documents/{command.Category.ToString().ToLowerInvariant()}",
                command.File.FileName,
                fileValidation.Value,
                command.File.Content,
                cancellationToken);

            oldFileKey = document.FileKey;
            document.ReplaceFile(
                stored.FileKey,
                stored.FileName,
                stored.ContentType,
                stored.SizeBytes,
                effectiveFileDate);
        }

        document.UpdateMetadata(
            command.CompanyId,
            command.ContractTypeId,
            command.Category,
            targetIsWelcomeKit,
            command.Title,
            command.Description,
            effectiveFileDate);

        await documentRepository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldFileKey))
        {
            await fileStorageService.DeleteAsync(StorageScope.CommonPublic, oldFileKey, cancellationToken);
        }

        return MapToDto(document);
    }

    public async Task<Result> ArchiveAsync(
        Guid id,
        ArchiveCommonDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("CommonDocuments.NotFound", $"Common document '{id}' was not found."));
        }

        if (!CanWrite(document.Category, document.IsWelcomeKit))
        {
            return Result.Failure(Error.Forbidden(
                "CommonDocuments.WriteForbidden",
                "You do not have permission to archive this common document."));
        }

        var rowVersionResult = ValidateRowVersion(request.RowVersion, document.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult;
        }

        document.SetArchived(request.IsArchived);
        await documentRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("CommonDocuments.NotFound", $"Common document '{id}' was not found."));
        }

        if (!CanWrite(document.Category, document.IsWelcomeKit))
        {
            return Result.Failure(Error.Forbidden(
                "CommonDocuments.WriteForbidden",
                "You do not have permission to delete this common document."));
        }

        var fileKey = document.FileKey;
        documentRepository.Remove(document);
        await documentRepository.SaveChangesAsync(cancellationToken);

        await fileStorageService.DeleteAsync(StorageScope.CommonPublic, fileKey, cancellationToken);
        return Result.Success();
    }

    private bool CanRead() =>
        HasPermission(InternalLifePermissions.CommonDocsRead) ||
        HasPermission(InternalLifePermissions.WelcomeKitsRead);

    private bool CanWrite(CommonDocumentCategory category, bool isWelcomeKit)
    {
        if (HasPermission(InternalLifePermissions.CommonDocsWrite))
        {
            return true;
        }

        if (isWelcomeKit && HasPermission(InternalLifePermissions.WelcomeKitsWrite))
        {
            return true;
        }

        return !isWelcomeKit &&
               CommonDocumentTaxonomy.IsCseCategory(category) &&
               HasPermission(InternalLifePermissions.CommonDocsWriteCse);
    }

    private bool HasPermission(string permission) =>
        currentUser.IsAuthenticated &&
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<Result> ValidateReferencesAsync(
        Guid? companyId,
        Guid? contractTypeId,
        CancellationToken cancellationToken)
    {
        if (companyId.HasValue)
        {
            var company = await organizationLookup.GetCompanyByIdAsync(companyId.Value, cancellationToken);
            if (company is null)
            {
                return Result.Failure(Error.NotFound("Companies.NotFound", $"Company '{companyId.Value}' was not found."));
            }
        }

        if (contractTypeId.HasValue &&
            !await organizationLookup.ContractTypeExistsAsync(contractTypeId.Value, cancellationToken))
        {
            return Result.Failure(Error.NotFound(
                "ContractTypes.NotFound",
                $"Contract type '{contractTypeId.Value}' was not found."));
        }

        return Result.Success();
    }

    private async Task<Result<string>> ValidateUploadedFileAsync(
        UploadedCommonFileInput? file,
        CancellationToken cancellationToken)
    {
        if (file is null || string.IsNullOrWhiteSpace(file.FileName))
        {
            return Error.Validation("File.Required", "A valid file is required.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!ExtensionMimeTypes.TryGetValue(extension, out var normalizedContentType))
        {
            return Error.Validation(
                "File.UnsupportedFormat",
                "Only PDF, PNG, JPEG and WEBP files are accepted.");
        }

        if (file.Content.CanSeek)
        {
            file.Content.Position = 0;
        }

        var validation = await fileValidator.ValidateAsync(
            file.Content,
            file.FileName,
            normalizedContentType,
            cancellationToken);

        if (file.Content.CanSeek)
        {
            file.Content.Position = 0;
        }

        if (!validation.IsValid)
        {
            return Error.Validation(
                "File.Invalid",
                validation.ErrorMessage ?? "The uploaded file content is invalid.");
        }

        return normalizedContentType;
    }

    private static Result ValidateRowVersion(string? rowVersionBase64, byte[] currentRowVersion)
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
                "The resource was modified concurrently. Please refresh and retry."));
        }

        return Result.Success();
    }

    private static CommonDocumentDto MapToDto(CommonDocument document) =>
        new(
            document.Id,
            document.CompanyId,
            document.ContractTypeId,
            document.Category,
            document.IsWelcomeKit,
            document.Title,
            document.Description,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.FileDate,
            document.IsArchived,
            document.CreatedAtUtc,
            document.UpdatedAtUtc,
            Convert.ToBase64String(document.RowVersion));
}
