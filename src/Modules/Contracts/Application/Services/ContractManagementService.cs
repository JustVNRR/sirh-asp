using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Constants;
using Sirh.Modules.Contracts.Domain.Entities;
using Sirh.Modules.Contracts.Domain.Enums;

namespace Sirh.Modules.Contracts.Application.Services;

public sealed class ContractManagementService(
    IContractRepository contractRepository,
    IOrganizationLookup organizationLookup,
    IHumanResourcesLookup humanResourcesLookup,
    IFileValidator fileValidator,
    IFileStorageService fileStorageService,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser) : IContractManagementService
{
    public async Task<Result<PagedResult<ContractDto>>> GetPagedAsync(
        Guid? employeeId,
        ContractStatus? status,
        Guid? contractTypeId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var canReadAll = HasPermission(ContractPermissions.ContractsReadAll);
        var canReadSelf = HasPermission(ContractPermissions.ContractsReadSelf);

        if (!canReadAll && !canReadSelf)
        {
            return Error.Forbidden("Contracts.Forbidden", "You do not have permission to view contracts.");
        }

        var effectiveEmployeeId = employeeId;
        if (!canReadAll)
        {
            if (!currentUser.EmployeeId.HasValue)
            {
                return Error.Forbidden("Contracts.Forbidden", "Only linked employees or authorized HR users can view contracts.");
            }

            if (employeeId.HasValue && employeeId.Value != currentUser.EmployeeId.Value)
            {
                return Error.Forbidden("Contracts.Forbidden", "You can only view your own contracts.");
            }

            effectiveEmployeeId = currentUser.EmployeeId.Value;
        }

        var page = await contractRepository.GetPagedAsync(
            effectiveEmployeeId,
            status,
            contractTypeId,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(MapToDto).ToArray();
        return PagedResult<ContractDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<ContractDto>> GetByPositionIdAsync(
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var contract = await contractRepository.GetByPositionIdAsync(positionId, cancellationToken);
        if (contract is null)
        {
            return Error.NotFound(
                "Contracts.NotFound",
                $"No contract was found for position '{positionId}'.");
        }

        if (!CanReadContract(contract.EmployeeId))
        {
            return Error.Forbidden("Contracts.Forbidden", "You do not have permission to view this contract.");
        }

        return MapToDto(contract);
    }

    public async Task<Result<DownloadedFileDto>> OpenFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var contract = await contractRepository.GetByIdAsync(id, cancellationToken);
        if (contract is null)
        {
            return Error.NotFound("Contracts.NotFound", $"Contract '{id}' was not found.");
        }

        if (!CanReadContract(contract.EmployeeId))
        {
            return Error.Forbidden("Contracts.Forbidden", "You do not have permission to download this contract.");
        }

        var stream = await fileStorageService.OpenReadAsync(
            StorageScope.EmployeeSensitive,
            contract.FileKey,
            cancellationToken);

        return new DownloadedFileDto(stream, contract.FileName, contract.ContentType, contract.FileSizeBytes);
    }

    public async Task<Result<ContractDto>> ImportAsync(
        ImportContractCommand command,
        CancellationToken cancellationToken = default)
    {
        var fileValidation = await PdfUploadValidator.ValidateAsync(command.File, fileValidator, cancellationToken);
        if (fileValidation.IsFailure)
        {
            return fileValidation.Error;
        }

        if (!await organizationLookup.ContractTypeExistsAsync(command.ContractTypeId, cancellationToken))
        {
            return Error.NotFound(
                "ContractTypes.NotFound",
                $"Contract type '{command.ContractTypeId}' was not found.");
        }

        var positionContext = await humanResourcesLookup.GetPositionContractContextAsync(command.PositionId, cancellationToken);
        if (positionContext is null)
        {
            return Error.NotFound(
                "Positions.NotFound",
                $"Position '{command.PositionId}' was not found.");
        }

        if (await contractRepository.ExistsByPositionIdAsync(command.PositionId, cancellationToken))
        {
            return Error.Conflict(
                "Contracts.PositionAlreadyHasContract",
                $"Position '{command.PositionId}' already has an associated contract.");
        }

        var subFolder = $"employees/{positionContext.EmployeeId}/contracts";
        var stored = await fileStorageService.SaveAsync(
            StorageScope.EmployeeSensitive,
            subFolder,
            command.File.FileName,
            "application/pdf",
            command.File.Content,
            cancellationToken);

        var signedAt = ResolveSignedAt(command.Status, command.SignedAt, existingSignedAt: null);
        var contract = new Contract(
            command.PositionId,
            positionContext.EmployeeId,
            command.ContractTypeId,
            contractModelId: null,
            command.Status,
            signedAt,
            stored.FileKey,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            dateTimeProvider.TodayUtc,
            command.Commentary);

        await contractRepository.AddAsync(contract, cancellationToken);
        await contractRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(contract);
    }

    public async Task<Result<ContractDto>> UpdateAsync(
        Guid id,
        UpdateContractCommand command,
        CancellationToken cancellationToken = default)
    {
        var contract = await contractRepository.GetByIdAsync(id, cancellationToken);
        if (contract is null)
        {
            return Error.NotFound("Contracts.NotFound", $"Contract '{id}' was not found.");
        }

        var rowVersionResult = ContractRenderContextBuilder.ValidateRowVersion(command.RowVersion, contract.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        if (!await organizationLookup.ContractTypeExistsAsync(command.ContractTypeId, cancellationToken))
        {
            return Error.NotFound(
                "ContractTypes.NotFound",
                $"Contract type '{command.ContractTypeId}' was not found.");
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
                $"employees/{contract.EmployeeId}/contracts",
                command.File.FileName,
                "application/pdf",
                command.File.Content,
                cancellationToken);

            oldFileKey = contract.FileKey;
            contract.ReplaceFile(
                stored.FileKey,
                stored.FileName,
                stored.ContentType,
                stored.SizeBytes,
                dateTimeProvider.TodayUtc);
        }

        var signedAt = ResolveSignedAt(command.Status, command.SignedAt, contract.SignedAt);
        contract.UpdateMetadata(command.ContractTypeId, command.Status, signedAt, command.Commentary);

        await contractRepository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldFileKey))
        {
            await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, oldFileKey, cancellationToken);
        }

        return MapToDto(contract);
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var contract = await contractRepository.GetByIdAsync(id, cancellationToken);
        if (contract is null)
        {
            return Result.Failure(Error.NotFound("Contracts.NotFound", $"Contract '{id}' was not found."));
        }

        var fileKey = contract.FileKey;
        contractRepository.Remove(contract);
        await contractRepository.SaveChangesAsync(cancellationToken);

        await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, fileKey, cancellationToken);
        return Result.Success();
    }

    private DateOnly? ResolveSignedAt(ContractStatus status, DateOnly? requestedSignedAt, DateOnly? existingSignedAt) =>
        status == ContractStatus.Signed
            ? requestedSignedAt ?? existingSignedAt ?? dateTimeProvider.TodayUtc
            : requestedSignedAt;

    private bool CanReadContract(Guid employeeId)
    {
        if (HasPermission(ContractPermissions.ContractsReadAll))
        {
            return true;
        }

        return HasPermission(ContractPermissions.ContractsReadSelf)
               && currentUser.EmployeeId.HasValue
               && currentUser.EmployeeId.Value == employeeId;
    }

    private bool HasPermission(string permission) =>
        currentUser.IsAuthenticated &&
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    internal static ContractDto MapToDto(Contract contract) =>
        new(
            contract.Id,
            contract.PositionId,
            contract.EmployeeId,
            contract.ContractTypeId,
            contract.ContractModelId,
            contract.Status,
            contract.SignedAt,
            contract.FileName,
            contract.ContentType,
            contract.FileSizeBytes,
            contract.FileDate,
            contract.Commentary,
            contract.CreatedAtUtc,
            contract.UpdatedAtUtc,
            Convert.ToBase64String(contract.RowVersion));
}
