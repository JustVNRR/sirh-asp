using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Constants;
using Sirh.Modules.Contracts.Domain.Entities;

namespace Sirh.Modules.Contracts.Application.Services;

public sealed class JobSheetService(
    IJobSheetRepository jobSheetRepository,
    IHumanResourcesLookup humanResourcesLookup,
    IFileValidator fileValidator,
    IFileStorageService fileStorageService,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser) : IJobSheetService
{
    public async Task<Result<JobSheetDto>> GetByPositionIdAsync(
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var jobSheet = await jobSheetRepository.GetByPositionIdAsync(positionId, cancellationToken);
        if (jobSheet is null)
        {
            return Error.NotFound(
                "JobSheets.NotFound",
                $"No job sheet was found for position '{positionId}'.");
        }

        if (!await CanReadJobSheetAsync(jobSheet.EmployeeId, cancellationToken))
        {
            return Error.Forbidden(
                "JobSheets.Forbidden",
                "You do not have permission to view this job sheet.");
        }

        return MapToDto(jobSheet);
    }

    public async Task<Result<DownloadedFileDto>> OpenFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var jobSheet = await jobSheetRepository.GetByIdAsync(id, cancellationToken);
        if (jobSheet is null)
        {
            return Error.NotFound("JobSheets.NotFound", $"Job sheet '{id}' was not found.");
        }

        if (!await CanReadJobSheetAsync(jobSheet.EmployeeId, cancellationToken))
        {
            return Error.Forbidden(
                "JobSheets.Forbidden",
                "You do not have permission to download this job sheet.");
        }

        var stream = await fileStorageService.OpenReadAsync(
            StorageScope.EmployeeSensitive,
            jobSheet.FileKey,
            cancellationToken);

        return new DownloadedFileDto(stream, jobSheet.FileName, jobSheet.ContentType, jobSheet.FileSizeBytes);
    }

    public async Task<Result<JobSheetDto>> CreateAsync(
        CreateJobSheetCommand command,
        CancellationToken cancellationToken = default)
    {
        var fileValidation = await PdfUploadValidator.ValidateAsync(command.File, fileValidator, cancellationToken);
        if (fileValidation.IsFailure)
        {
            return fileValidation.Error;
        }

        var positionContext = await humanResourcesLookup.GetPositionContractContextAsync(command.PositionId, cancellationToken);
        if (positionContext is null)
        {
            return Error.NotFound(
                "Positions.NotFound",
                $"Position '{command.PositionId}' was not found.");
        }

        if (await jobSheetRepository.ExistsByPositionIdAsync(command.PositionId, cancellationToken))
        {
            return Error.Conflict(
                "JobSheets.PositionAlreadyHasJobSheet",
                $"Position '{command.PositionId}' already has an associated job sheet.");
        }

        var stored = await fileStorageService.SaveAsync(
            StorageScope.EmployeeSensitive,
            $"employees/{positionContext.EmployeeId}/job-sheets",
            command.File.FileName,
            "application/pdf",
            command.File.Content,
            cancellationToken);

        var jobSheet = new JobSheet(
            command.PositionId,
            positionContext.EmployeeId,
            stored.FileKey,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            dateTimeProvider.TodayUtc,
            command.Commentary);

        await jobSheetRepository.AddAsync(jobSheet, cancellationToken);
        await jobSheetRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(jobSheet);
    }

    public async Task<Result<JobSheetDto>> UpdateAsync(
        Guid id,
        UpdateJobSheetCommand command,
        CancellationToken cancellationToken = default)
    {
        var jobSheet = await jobSheetRepository.GetByIdAsync(id, cancellationToken);
        if (jobSheet is null)
        {
            return Error.NotFound("JobSheets.NotFound", $"Job sheet '{id}' was not found.");
        }

        var rowVersionResult = ContractRenderContextBuilder.ValidateRowVersion(command.RowVersion, jobSheet.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        string? oldFileKey = null;
        if (command.File is not null)
        {
            var fileValidation = await PdfUploadValidator.ValidateAsync(command.File, fileValidator, cancellationToken);
            if (fileValidation.IsFailure)
            {
                return fileValidation.Error;
            }

            var stored = await fileStorageService.SaveAsync(
                StorageScope.EmployeeSensitive,
                $"employees/{jobSheet.EmployeeId}/job-sheets",
                command.File.FileName,
                "application/pdf",
                command.File.Content,
                cancellationToken);

            oldFileKey = jobSheet.FileKey;
            jobSheet.ReplaceFile(
                stored.FileKey,
                stored.FileName,
                stored.ContentType,
                stored.SizeBytes,
                dateTimeProvider.TodayUtc);
        }

        jobSheet.UpdateMetadata(command.Commentary);
        await jobSheetRepository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldFileKey))
        {
            await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, oldFileKey, cancellationToken);
        }

        return MapToDto(jobSheet);
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var jobSheet = await jobSheetRepository.GetByIdAsync(id, cancellationToken);
        if (jobSheet is null)
        {
            return Result.Failure(Error.NotFound("JobSheets.NotFound", $"Job sheet '{id}' was not found."));
        }

        var fileKey = jobSheet.FileKey;
        jobSheetRepository.Remove(jobSheet);
        await jobSheetRepository.SaveChangesAsync(cancellationToken);

        await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, fileKey, cancellationToken);
        return Result.Success();
    }

    private async Task<bool> CanReadJobSheetAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (HasPermission(ContractPermissions.JobSheetsReadAll))
        {
            return true;
        }

        if (!currentUser.EmployeeId.HasValue)
        {
            return false;
        }

        if (HasPermission(ContractPermissions.JobSheetsReadSelf)
            && currentUser.EmployeeId.Value == employeeId)
        {
            return true;
        }

        if (HasPermission(ContractPermissions.JobSheetsReadTeam))
        {
            return await humanResourcesLookup.IsManagerOfEmployeeAsync(
                currentUser.EmployeeId.Value,
                employeeId,
                cancellationToken);
        }

        return false;
    }

    private bool HasPermission(string permission) =>
        currentUser.IsAuthenticated &&
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static JobSheetDto MapToDto(JobSheet jobSheet) =>
        new(
            jobSheet.Id,
            jobSheet.PositionId,
            jobSheet.EmployeeId,
            jobSheet.FileName,
            jobSheet.ContentType,
            jobSheet.FileSizeBytes,
            jobSheet.FileDate,
            jobSheet.Commentary,
            jobSheet.CreatedAtUtc,
            jobSheet.UpdatedAtUtc,
            Convert.ToBase64String(jobSheet.RowVersion));
}
