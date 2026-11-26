using FluentValidation;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Organization.Application.Abstractions;
using Sirh.Modules.Organization.Application.DTOs;
using Sirh.Modules.Organization.Domain.Entities;

namespace Sirh.Modules.Organization.Application.Services;

public sealed class ContractTypeService(
    IContractTypeRepository contractTypeRepository,
    IEnumerable<IContractTypeReferenceChecker> referenceCheckers,
    IValidator<CreateContractTypeRequest> createValidator,
    IValidator<UpdateContractTypeRequest> updateValidator) : IContractTypeService
{
    public async Task<Result<PagedResult<ContractTypeDto>>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var paged = await contractTypeRepository.GetPagedAsync(isActive, search, pagedRequest, cancellationToken);
        var items = paged.Items.Select(ToDto).ToArray();
        return PagedResult<ContractTypeDto>.Create(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<Result<ContractTypeDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contractType = await contractTypeRepository.GetByIdAsync(id, cancellationToken);
        return contractType is null
            ? Error.NotFound("ContractTypes.NotFound", $"Contract type '{id}' was not found.")
            : ToDto(contractType);
    }

    public async Task<Result<ContractTypeDto>> CreateAsync(
        CreateContractTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var code = ContractType.NormalizeCode(request.Code);
        if (await contractTypeRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            return Error.Conflict("ContractTypes.DuplicateCode", $"Contract type code '{code}' already exists.");
        }

        var contractType = new ContractType(code, request.Label, isSystem: false, isActive: request.IsActive);
        await contractTypeRepository.AddAsync(contractType, cancellationToken);
        await contractTypeRepository.SaveChangesAsync(cancellationToken);

        return ToDto(contractType);
    }

    public async Task<Result<ContractTypeDto>> UpdateAsync(
        Guid id,
        UpdateContractTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var contractType = await contractTypeRepository.GetByIdAsync(id, cancellationToken);
        if (contractType is null)
        {
            return Error.NotFound("ContractTypes.NotFound", $"Contract type '{id}' was not found.");
        }

        var concurrency = ConcurrencyHelper.ValidateRowVersion(request.RowVersion, contractType.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        var normalizedCode = ContractType.NormalizeCode(request.Code);
        if (contractType.IsSystem && !string.Equals(contractType.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Forbidden(
                "ContractTypes.SystemCodeImmutable",
                "The code of a system contract type cannot be modified.");
        }

        if (await contractTypeRepository.ExistsByCodeAsync(normalizedCode, id, cancellationToken))
        {
            return Error.Conflict("ContractTypes.DuplicateCode", $"Contract type code '{normalizedCode}' already exists.");
        }

        contractType.Update(normalizedCode, request.Label, request.IsActive);
        await contractTypeRepository.SaveChangesAsync(cancellationToken);

        return ToDto(contractType);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contractType = await contractTypeRepository.GetByIdAsync(id, cancellationToken);
        if (contractType is null)
        {
            return Result.Failure(Error.NotFound("ContractTypes.NotFound", $"Contract type '{id}' was not found."));
        }

        if (contractType.IsSystem)
        {
            return Result.Failure(Error.Forbidden(
                "ContractTypes.SystemProtected",
                "System contract types cannot be deleted."));
        }

        foreach (var checker in referenceCheckers)
        {
            if (await checker.IsContractTypeReferencedAsync(id, cancellationToken))
            {
                return Result.Failure(Error.Conflict(
                    "ContractTypes.InUse",
                    $"Contract type '{id}' cannot be deleted because it is used by existing records."));
            }
        }

        contractTypeRepository.Remove(contractType);
        await contractTypeRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static ContractTypeDto ToDto(ContractType ct) =>
        new(
            ct.Id,
            ct.Code,
            ct.Label,
            ct.IsSystem,
            ct.IsActive,
            ct.CreatedAtUtc,
            ct.UpdatedAtUtc,
            Convert.ToBase64String(ct.RowVersion));
}
