using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Organization.Domain.Entities;

namespace Sirh.Modules.Organization.Application.Abstractions;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Company>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySiretAsync(string siret, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Company company, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Location>> GetPagedAsync(
        bool? isPublished,
        Guid? companyId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Location location, CancellationToken cancellationToken = default);

    void Remove(Location location);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IContractTypeRepository
{
    Task<ContractType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<ContractType>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(ContractType contractType, CancellationToken cancellationToken = default);

    void Remove(ContractType contractType);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ILocationReferenceChecker
{
    Task<bool> IsLocationReferencedAsync(Guid locationId, CancellationToken cancellationToken = default);
}

public interface IContractTypeReferenceChecker
{
    Task<bool> IsContractTypeReferencedAsync(Guid contractTypeId, CancellationToken cancellationToken = default);
}
