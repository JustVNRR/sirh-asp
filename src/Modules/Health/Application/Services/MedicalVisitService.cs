using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Constants;
using Sirh.Modules.Health.Domain.Entities;

namespace Sirh.Modules.Health.Application.Services;

public sealed class MedicalVisitService(
    IMedicalVisitRepository visitRepository,
    IHumanResourcesLookup humanResourcesLookup,
    IFileValidator fileValidator,
    IFileStorageService fileStorageService,
    ICurrentUser currentUser) : IMedicalVisitService
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

    public async Task<Result<PagedResult<MedicalVisitDto>>> GetPagedAsync(
        MedicalVisitQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var canReadAll = HasPermission(HealthPermissions.MedicalVisitsReadAll);
        var canReadSelf = HasPermission(HealthPermissions.MedicalVisitsReadSelf);

        if (!canReadAll && !canReadSelf)
        {
            return Error.Forbidden(
                "MedicalVisits.Forbidden",
                "You do not have permission to view medical visits.");
        }

        var effectiveEmployeeId = query.EmployeeId;
        if (!canReadAll)
        {
            if (!currentUser.EmployeeId.HasValue)
            {
                return Error.Forbidden(
                    "MedicalVisits.Forbidden",
                    "Your account must be linked to an employee profile to view medical visits.");
            }

            if (query.EmployeeId.HasValue && query.EmployeeId.Value != currentUser.EmployeeId.Value)
            {
                return Error.Forbidden(
                    "MedicalVisits.Forbidden",
                    "You can only view your own medical visits.");
            }

            effectiveEmployeeId = currentUser.EmployeeId.Value;
        }

        var page = await visitRepository.GetPagedAsync(
            effectiveEmployeeId,
            query.VisitType,
            query.FromDateUtc,
            query.ToDateUtc,
            query.CarriedOut,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(MapToDto).ToArray();
        return PagedResult<MedicalVisitDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<MedicalVisitDto>> GetByIdAsync(
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

    public async Task<Result<DownloadedHealthFileDto>> OpenConvocationFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await LoadAndAuthorizeReadAsync(id, cancellationToken);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        var visit = accessResult.Value;
        var stream = await fileStorageService.OpenReadAsync(
            StorageScope.EmployeeSensitive,
            visit.ConvocationFileKey,
            cancellationToken);

        return new DownloadedHealthFileDto(
            stream,
            visit.ConvocationFileName,
            visit.ConvocationContentType,
            visit.ConvocationFileSizeBytes);
    }

    public async Task<Result<DownloadedHealthFileDto>> OpenReportFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await LoadAndAuthorizeReadAsync(id, cancellationToken);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        var visit = accessResult.Value;
        if (string.IsNullOrWhiteSpace(visit.ReportFileKey) ||
            string.IsNullOrWhiteSpace(visit.ReportFileName) ||
            string.IsNullOrWhiteSpace(visit.ReportContentType) ||
            !visit.ReportFileSizeBytes.HasValue)
        {
            return Error.NotFound(
                "MedicalVisits.ReportNotFound",
                $"Medical visit '{id}' does not have an attached report.");
        }

        var stream = await fileStorageService.OpenReadAsync(
            StorageScope.EmployeeSensitive,
            visit.ReportFileKey,
            cancellationToken);

        return new DownloadedHealthFileDto(
            stream,
            visit.ReportFileName,
            visit.ReportContentType,
            visit.ReportFileSizeBytes.Value);
    }

    public async Task<Result<MedicalVisitDto>> CreateAsync(
        CreateMedicalVisitCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.EmployeeId == Guid.Empty)
        {
            return Error.Validation("MedicalVisits.EmployeeIdRequired", "EmployeeId is required.");
        }

        if (!Enum.IsDefined(command.VisitType))
        {
            return Error.Validation("MedicalVisits.InvalidVisitType", $"Unsupported visit type '{command.VisitType}'.");
        }

        var convocationValidation = await ValidateUploadedFileAsync(command.ConvocationFile, cancellationToken);
        if (convocationValidation.IsFailure)
        {
            return convocationValidation.Error;
        }

        string? reportMimeType = null;
        if (command.ReportFile is not null)
        {
            var reportValidation = await ValidateUploadedFileAsync(command.ReportFile, cancellationToken);
            if (reportValidation.IsFailure)
            {
                return reportValidation.Error;
            }

            reportMimeType = reportValidation.Value;
        }

        if (!await humanResourcesLookup.EmployeeExistsAsync(command.EmployeeId, cancellationToken))
        {
            return Error.NotFound(
                "Employees.NotFound",
                $"Employee '{command.EmployeeId}' was not found.");
        }

        var subFolder = $"employees/{command.EmployeeId}/medical-visits";
        var convocationStored = await fileStorageService.SaveAsync(
            StorageScope.EmployeeSensitive,
            subFolder,
            command.ConvocationFile.FileName,
            convocationValidation.Value,
            command.ConvocationFile.Content,
            cancellationToken);

        var visit = new MedicalVisit(
            command.EmployeeId,
            command.VisitType,
            command.VisitDateTimeUtc,
            command.ValidityDate,
            command.MedicalCenter,
            command.CarriedOut,
            command.Comments,
            convocationStored.FileKey,
            convocationStored.FileName,
            convocationStored.ContentType,
            convocationStored.SizeBytes);

        if (command.ReportFile is not null)
        {
            var reportStored = await fileStorageService.SaveAsync(
                StorageScope.EmployeeSensitive,
                subFolder,
                command.ReportFile.FileName,
                reportMimeType!,
                command.ReportFile.Content,
                cancellationToken);

            visit.SetReportFile(
                reportStored.FileKey,
                reportStored.FileName,
                reportStored.ContentType,
                reportStored.SizeBytes);
        }

        await visitRepository.AddAsync(visit, cancellationToken);
        await visitRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(visit);
    }

    public async Task<Result<MedicalVisitDto>> UpdateAsync(
        Guid id,
        UpdateMedicalVisitCommand command,
        CancellationToken cancellationToken = default)
    {
        var visit = await visitRepository.GetByIdAsync(id, cancellationToken);
        if (visit is null)
        {
            return Error.NotFound("MedicalVisits.NotFound", $"Medical visit '{id}' was not found.");
        }

        var rowVersionResult = ValidateRowVersion(command.RowVersion, visit.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        if (!Enum.IsDefined(command.VisitType))
        {
            return Error.Validation("MedicalVisits.InvalidVisitType", $"Unsupported visit type '{command.VisitType}'.");
        }

        string? convocationMime = null;
        if (command.ConvocationFile is not null)
        {
            var convValidation = await ValidateUploadedFileAsync(command.ConvocationFile, cancellationToken);
            if (convValidation.IsFailure)
            {
                return convValidation.Error;
            }

            convocationMime = convValidation.Value;
        }

        string? reportMime = null;
        if (command.ReportFile is not null)
        {
            var reportValidation = await ValidateUploadedFileAsync(command.ReportFile, cancellationToken);
            if (reportValidation.IsFailure)
            {
                return reportValidation.Error;
            }

            reportMime = reportValidation.Value;
        }

        var subFolder = $"employees/{visit.EmployeeId}/medical-visits";
        var oldFilesToDelete = new List<string>();

        if (command.ConvocationFile is not null)
        {
            var stored = await fileStorageService.SaveAsync(
                StorageScope.EmployeeSensitive,
                subFolder,
                command.ConvocationFile.FileName,
                convocationMime!,
                command.ConvocationFile.Content,
                cancellationToken);

            oldFilesToDelete.Add(visit.ConvocationFileKey);
            visit.ReplaceConvocationFile(stored.FileKey, stored.FileName, stored.ContentType, stored.SizeBytes);
        }

        if (command.ReportFile is not null)
        {
            var stored = await fileStorageService.SaveAsync(
                StorageScope.EmployeeSensitive,
                subFolder,
                command.ReportFile.FileName,
                reportMime!,
                command.ReportFile.Content,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(visit.ReportFileKey))
            {
                oldFilesToDelete.Add(visit.ReportFileKey);
            }

            visit.SetReportFile(stored.FileKey, stored.FileName, stored.ContentType, stored.SizeBytes);
        }

        visit.UpdateMetadata(
            command.VisitType,
            command.VisitDateTimeUtc,
            command.ValidityDate,
            command.MedicalCenter,
            command.CarriedOut,
            command.Comments);

        await visitRepository.SaveChangesAsync(cancellationToken);

        foreach (var oldKey in oldFilesToDelete)
        {
            await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, oldKey, cancellationToken);
        }

        return MapToDto(visit);
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var visit = await visitRepository.GetByIdAsync(id, cancellationToken);
        if (visit is null)
        {
            return Result.Failure(Error.NotFound("MedicalVisits.NotFound", $"Medical visit '{id}' was not found."));
        }

        var filesToDelete = new List<string> { visit.ConvocationFileKey };
        if (!string.IsNullOrWhiteSpace(visit.ReportFileKey))
        {
            filesToDelete.Add(visit.ReportFileKey);
        }

        visitRepository.Remove(visit);
        await visitRepository.SaveChangesAsync(cancellationToken);

        foreach (var fileKey in filesToDelete)
        {
            await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, fileKey, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result<MedicalVisit>> LoadAndAuthorizeReadAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var visit = await visitRepository.GetByIdAsync(id, cancellationToken);
        if (visit is null)
        {
            return Error.NotFound("MedicalVisits.NotFound", $"Medical visit '{id}' was not found.");
        }

        var canReadAll = HasPermission(HealthPermissions.MedicalVisitsReadAll);
        var canReadSelf = HasPermission(HealthPermissions.MedicalVisitsReadSelf);

        if (canReadAll ||
            (canReadSelf && currentUser.EmployeeId.HasValue && currentUser.EmployeeId.Value == visit.EmployeeId))
        {
            return visit;
        }

        return Error.Forbidden(
            "MedicalVisits.Forbidden",
            "You do not have permission to access this medical visit.");
    }

    private bool HasPermission(string permission) =>
        currentUser.IsAuthenticated &&
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<Result<string>> ValidateUploadedFileAsync(
        UploadedHealthFileInput? file,
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

    private static MedicalVisitDto MapToDto(MedicalVisit visit) =>
        new(
            visit.Id,
            visit.EmployeeId,
            visit.VisitType,
            visit.VisitDateTimeUtc,
            visit.ValidityDate,
            visit.MedicalCenter,
            visit.CarriedOut,
            visit.Comments,
            visit.ConvocationFileName,
            visit.ConvocationContentType,
            visit.ConvocationFileSizeBytes,
            !string.IsNullOrWhiteSpace(visit.ReportFileKey),
            visit.ReportFileName,
            visit.ReportContentType,
            visit.ReportFileSizeBytes,
            visit.ReminderSentAtUtc,
            visit.CreatedAtUtc,
            visit.UpdatedAtUtc,
            Convert.ToBase64String(visit.RowVersion));
}
