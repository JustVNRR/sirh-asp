using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Organization.Application.DTOs;

namespace Sirh.Modules.Organization.Application.Abstractions;

public interface ICompanyService
{
    Task<Result<PagedResult<CompanyDto>>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<CompanyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<CompanyDto>> CreateAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CompanyDto>> UpdateAsync(
        Guid id,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CompanyDto>> UpdateStatusAsync(
        Guid id,
        UpdateCompanyStatusRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILocationService
{
    Task<Result<PagedResult<LocationDto>>> GetPagedAsync(
        bool? isPublished,
        Guid? companyId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<LocationDto>> CreateAsync(
        CreateLocationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LocationDto>> UpdateAsync(
        Guid id,
        UpdateLocationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IContractTypeService
{
    Task<Result<PagedResult<ContractTypeDto>>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<ContractTypeDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ContractTypeDto>> CreateAsync(
        CreateContractTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ContractTypeDto>> UpdateAsync(
        Guid id,
        UpdateContractTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
