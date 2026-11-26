using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;
using Sirh.Modules.HumanResources.Domain.Entities;
using Sirh.Modules.HumanResources.Domain.Enums;

namespace Sirh.Modules.HumanResources.Application.Services;

public sealed class EmployeeDocumentService(
    IEmployeeDocumentRepository documentRepository,
    IEmployeeRepository employeeRepository,
    IFileValidator fileValidator,
    IFileStorageService fileStorageService,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser) : IEmployeeDocumentService
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

    public async Task<Result<PagedResult<EmployeeDocumentDto>>> GetPagedAsync(
        EmployeeDocumentQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var canReadAll = HasPermission(HrPermissions.EmployeeDocsReadAll);
        var canReadTeam = HasPermission(HrPermissions.EmployeeDocsReadTeam);
        var canReadSelf = HasPermission(HrPermissions.EmployeeDocsReadSelf);
        var canReadSensitive = HasPermission(HrPermissions.EmployeeDocsReadSensitive);

        if (!canReadAll && !canReadTeam && !canReadSelf)
        {
            return Error.Forbidden(
                "EmployeeDocuments.Forbidden",
                "You do not have permission to view employee documents.");
        }

        if (!canReadSensitive &&
            ((query.Category.HasValue && EmployeeDocumentTaxonomy.IsSensitiveCategory(query.Category.Value)) ||
             (query.DocumentTypes is not null && query.DocumentTypes.Any(EmployeeDocumentTaxonomy.IsSensitiveType))))
        {
            return Error.Forbidden(
                "EmployeeDocuments.SensitiveForbidden",
                "You do not have permission to view sensitive employee documents.");
        }

        var scopeResult = await ResolveReadScopeAsync(
            query.EmployeeId,
            canReadAll,
            canReadTeam,
            canReadSelf,
            cancellationToken);

        if (scopeResult.IsFailure)
        {
            return scopeResult.Error;
        }

        var page = await documentRepository.GetPagedAsync(
            scopeResult.Value,
            excludeSensitive: !canReadSensitive,
            query.Category,
            query.DocumentTypes,
            query.Search,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(MapToDto).ToArray();
        return PagedResult<EmployeeDocumentDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<EmployeeDocumentDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await LoadAndAuthorizeReadAsync(id, cancellationToken);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        return MapToDto(accessResult.Value);
    }

    public async Task<Result<DownloadedEmployeeDocumentDto>> OpenFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await LoadAndAuthorizeReadAsync(id, cancellationToken);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        var document = accessResult.Value;
        var stream = await fileStorageService.OpenReadAsync(
            StorageScope.EmployeeSensitive,
            document.FileKey,
            cancellationToken);

        return new DownloadedEmployeeDocumentDto(
            stream,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes);
    }

    public async Task<Result<EmployeeDocumentDto>> CreateAsync(
        CreateEmployeeDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var targetEmployeeId = command.EmployeeId ?? currentUser.EmployeeId;
        if (!targetEmployeeId.HasValue || targetEmployeeId.Value == Guid.Empty)
        {
            return Error.Validation(
                "EmployeeDocuments.EmployeeIdRequired",
                "EmployeeId is required.");
        }

        var categoryResult = ResolveAndValidateCategory(command.Category, command.DocumentType);
        if (categoryResult.IsFailure)
        {
            return categoryResult.Error;
        }

        var expectedCategory = categoryResult.Value;
        if (!CanWriteDocument(targetEmployeeId.Value, expectedCategory))
        {
            return Error.Forbidden(
                "EmployeeDocuments.WriteForbidden",
                "You do not have permission to upload documents in this category or for this employee.");
        }

        var metadataResult = ValidateSpecializedMetadata(
            command.DocumentType,
            command.ExpiryDate,
            command.PermitNumber);
        if (metadataResult.IsFailure)
        {
            return metadataResult.Error;
        }

        var fileValidation = await ValidateUploadedFileAsync(command.File, cancellationToken);
        if (fileValidation.IsFailure)
        {
            return fileValidation.Error;
        }

        if (!await employeeRepository.ExistsAsync(targetEmployeeId.Value, cancellationToken))
        {
            return Error.NotFound(
                "Employees.NotFound",
                $"Employee '{targetEmployeeId.Value}' was not found.");
        }

        var normalizedContentType = fileValidation.Value;
        var stored = await fileStorageService.SaveAsync(
            StorageScope.EmployeeSensitive,
            $"employees/{targetEmployeeId.Value}/documents",
            command.File.FileName,
            normalizedContentType,
            command.File.Content,
            cancellationToken);

        var fileDate = command.FileDate ?? dateTimeProvider.TodayUtc;
        var document = new EmployeeDocument(
            targetEmployeeId.Value,
            command.DocumentType,
            stored.FileKey,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            fileDate,
            command.Commentary);

        ApplySpecializedMetadata(
            document,
            command.DocumentType,
            command.ExpiryDate,
            command.WorkplaceAccommodation,
            command.PermitNumber);

        await documentRepository.AddAsync(document, cancellationToken);
        await documentRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(document);
    }

    public async Task<Result<EmployeeDocumentDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Error.NotFound(
                "EmployeeDocuments.NotFound",
                $"Employee document '{id}' was not found.");
        }

        var categoryResult = ResolveAndValidateCategory(command.Category, command.DocumentType);
        if (categoryResult.IsFailure)
        {
            return categoryResult.Error;
        }

        var newCategory = categoryResult.Value;
        if (!CanWriteDocument(document.EmployeeId, document.Category) ||
            !CanWriteDocument(document.EmployeeId, newCategory))
        {
            return Error.Forbidden(
                "EmployeeDocuments.WriteForbidden",
                "You do not have permission to update this document.");
        }

        var rowVersionResult = HrConcurrencyHelper.ValidateRowVersion(command.RowVersion, document.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        var metadataResult = ValidateSpecializedMetadata(
            command.DocumentType,
            command.ExpiryDate,
            command.PermitNumber);
        if (metadataResult.IsFailure)
        {
            return metadataResult.Error;
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

            var normalizedContentType = fileValidation.Value;
            var stored = await fileStorageService.SaveAsync(
                StorageScope.EmployeeSensitive,
                $"employees/{document.EmployeeId}/documents",
                command.File.FileName,
                normalizedContentType,
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

        document.UpdateMetadata(command.DocumentType, effectiveFileDate, command.Commentary);
        ApplySpecializedMetadata(
            document,
            command.DocumentType,
            command.ExpiryDate,
            command.WorkplaceAccommodation,
            command.PermitNumber);

        await documentRepository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldFileKey))
        {
            await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, oldFileKey, cancellationToken);
        }

        return MapToDto(document);
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound(
                "EmployeeDocuments.NotFound",
                $"Employee document '{id}' was not found."));
        }

        if (!CanWriteDocument(document.EmployeeId, document.Category))
        {
            return Result.Failure(Error.Forbidden(
                "EmployeeDocuments.WriteForbidden",
                "You do not have permission to delete this document."));
        }

        var fileKey = document.FileKey;
        documentRepository.Remove(document);
        await documentRepository.SaveChangesAsync(cancellationToken);

        await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, fileKey, cancellationToken);
        return Result.Success();
    }

    private async Task<Result<IReadOnlyCollection<Guid>?>> ResolveReadScopeAsync(
        Guid? requestedEmployeeId,
        bool canReadAll,
        bool canReadTeam,
        bool canReadSelf,
        CancellationToken cancellationToken)
    {
        if (canReadAll)
        {
            IReadOnlyCollection<Guid>? filter = requestedEmployeeId.HasValue
                ? [requestedEmployeeId.Value]
                : null;
            return Result<IReadOnlyCollection<Guid>?>.Success(filter);
        }

        if (!currentUser.EmployeeId.HasValue)
        {
            return Error.Forbidden(
                "EmployeeDocuments.Forbidden",
                "Your account must be linked to an employee profile to view documents.");
        }

        var selfId = currentUser.EmployeeId.Value;

        if (canReadTeam)
        {
            var directReports = await employeeRepository.GetDirectReportIdsAsync(selfId, cancellationToken);
            var scope = new HashSet<Guid>(directReports) { selfId };

            if (requestedEmployeeId.HasValue)
            {
                if (!scope.Contains(requestedEmployeeId.Value))
                {
                    return Error.Forbidden(
                        "EmployeeDocuments.Forbidden",
                        "You can only view documents of yourself or your direct reports.");
                }

                return Result.Success<IReadOnlyCollection<Guid>?>([requestedEmployeeId.Value]);
            }

            return Result.Success<IReadOnlyCollection<Guid>?>(scope);
        }

        if (canReadSelf)
        {
            if (requestedEmployeeId.HasValue && requestedEmployeeId.Value != selfId)
            {
                return Error.Forbidden(
                    "EmployeeDocuments.Forbidden",
                    "You can only view your own employee documents.");
            }

            return Result.Success<IReadOnlyCollection<Guid>?>([selfId]);
        }

        return Error.Forbidden(
            "EmployeeDocuments.Forbidden",
            "You do not have permission to view employee documents.");
    }

    private async Task<Result<EmployeeDocument>> LoadAndAuthorizeReadAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Error.NotFound(
                "EmployeeDocuments.NotFound",
                $"Employee document '{id}' was not found.");
        }

        if (EmployeeDocumentTaxonomy.IsSensitiveCategory(document.Category) &&
            !HasPermission(HrPermissions.EmployeeDocsReadSensitive))
        {
            return Error.Forbidden(
                "EmployeeDocuments.SensitiveForbidden",
                "You do not have permission to access sensitive employee documents.");
        }

        if (HasPermission(HrPermissions.EmployeeDocsReadAll))
        {
            return document;
        }

        if (!currentUser.EmployeeId.HasValue)
        {
            return Error.Forbidden(
                "EmployeeDocuments.Forbidden",
                "You do not have permission to view this document.");
        }

        var selfId = currentUser.EmployeeId.Value;

        if (HasPermission(HrPermissions.EmployeeDocsReadSelf) && selfId == document.EmployeeId)
        {
            return document;
        }

        if (HasPermission(HrPermissions.EmployeeDocsReadTeam) &&
            (selfId == document.EmployeeId ||
             await employeeRepository.IsManagerOfAsync(selfId, document.EmployeeId, cancellationToken)))
        {
            return document;
        }

        return Error.Forbidden(
            "EmployeeDocuments.Forbidden",
            "You do not have permission to view this document.");
    }

    private bool CanWriteDocument(Guid targetEmployeeId, EmployeeDocumentCategory category)
    {
        if (HasPermission(HrPermissions.EmployeeDocsWriteAll))
        {
            return true;
        }

        return HasPermission(HrPermissions.EmployeeDocsWriteSelf)
               && currentUser.EmployeeId.HasValue
               && currentUser.EmployeeId.Value == targetEmployeeId
               && EmployeeDocumentTaxonomy.IsSelfServiceWritableCategory(category);
    }

    private bool HasPermission(string permission) =>
        currentUser.IsAuthenticated &&
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static Result<EmployeeDocumentCategory> ResolveAndValidateCategory(
        EmployeeDocumentCategory? requestedCategory,
        EmployeeDocumentType documentType)
    {
        if (!Enum.IsDefined(documentType))
        {
            return Error.Validation(
                "EmployeeDocuments.InvalidDocumentType",
                $"Unsupported document type '{documentType}'.");
        }

        var expected = EmployeeDocumentTaxonomy.GetCategory(documentType);
        if (requestedCategory.HasValue && requestedCategory.Value != expected)
        {
            return Error.Validation(
                "EmployeeDocuments.CategoryMismatch",
                $"Document type '{documentType}' belongs to category '{expected}', not '{requestedCategory.Value}'.");
        }

        return expected;
    }

    private static Result ValidateSpecializedMetadata(
        EmployeeDocumentType documentType,
        DateOnly? expiryDate,
        string? permitNumber)
    {
        switch (documentType)
        {
            case EmployeeDocumentType.RqthCertificate:
                if (!expiryDate.HasValue)
                {
                    return Result.Failure(Error.Validation(
                        "EmployeeDocuments.ExpiryDateRequired",
                        "ExpiryDate is required for RQTH certificates."));
                }

                break;

            case EmployeeDocumentType.ResidentPermit:
                if (!expiryDate.HasValue)
                {
                    return Result.Failure(Error.Validation(
                        "EmployeeDocuments.ExpiryDateRequired",
                        "ExpiryDate is required for resident permits."));
                }

                if (string.IsNullOrWhiteSpace(permitNumber))
                {
                    return Result.Failure(Error.Validation(
                        "EmployeeDocuments.PermitNumberRequired",
                        "PermitNumber is required for resident permits."));
                }

                break;
        }

        return Result.Success();
    }

    private static void ApplySpecializedMetadata(
        EmployeeDocument document,
        EmployeeDocumentType documentType,
        DateOnly? expiryDate,
        string? workplaceAccommodation,
        string? permitNumber)
    {
        switch (documentType)
        {
            case EmployeeDocumentType.RqthCertificate:
                document.SetRqthDetail(expiryDate!.Value, workplaceAccommodation);
                break;

            case EmployeeDocumentType.ResidentPermit:
                document.SetResidentPermitDetail(permitNumber!, expiryDate!.Value);
                break;

            default:
                document.ClearSpecializedDetails();
                break;
        }
    }

    private async Task<Result<string>> ValidateUploadedFileAsync(
        UploadedDocumentFileInput? file,
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

    private static EmployeeDocumentDto MapToDto(EmployeeDocument document) =>
        new(
            document.Id,
            document.EmployeeId,
            document.Category,
            document.DocumentType,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.FileDate,
            document.Commentary,
            document.RqthDetail is null
                ? null
                : new RqthDetailDto(
                    document.RqthDetail.ExpiryDate,
                    document.RqthDetail.WorkplaceAccommodation,
                    document.RqthDetail.ExpiryReminder30SentAtUtc,
                    document.RqthDetail.ExpiryReminder7SentAtUtc),
            document.ResidentPermitDetail is null
                ? null
                : new ResidentPermitDetailDto(
                    document.ResidentPermitDetail.PermitNumber,
                    document.ResidentPermitDetail.ExpiryDate,
                    document.ResidentPermitDetail.ExpiryReminder30SentAtUtc,
                    document.ResidentPermitDetail.ExpiryReminder7SentAtUtc),
            document.CreatedAtUtc,
            document.UpdatedAtUtc,
            Convert.ToBase64String(document.RowVersion));
}
