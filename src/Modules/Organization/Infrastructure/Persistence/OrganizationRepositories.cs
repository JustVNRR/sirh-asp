using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Organization.Application.Abstractions;
using Sirh.Modules.Organization.Domain.Entities;

namespace Sirh.Modules.Organization.Infrastructure.Persistence;

public sealed class CompanyRepository(OrganizationDbContext dbContext) : ICompanyRepository
{
    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<PagedResult<Company>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Companies.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.Siret.ToLower().Contains(term) ||
                (c.City != null && c.City.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<Company>.Create(items, totalCount, pagedRequest.NormalizedPage, pagedRequest.NormalizedPageSize);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Companies.AnyAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return dbContext.Companies.AnyAsync(
            c => c.Name.ToLower() == normalized && (!excludeId.HasValue || c.Id != excludeId.Value),
            cancellationToken);
    }

    public Task<bool> ExistsBySiretAsync(string siret, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = siret.Trim();
        return dbContext.Companies.AnyAsync(
            c => c.Siret == normalized && (!excludeId.HasValue || c.Id != excludeId.Value),
            cancellationToken);
    }

    public Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return dbContext.Companies.AnyAsync(
            c => c.Slug.ToLower() == normalized && (!excludeId.HasValue || c.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default) =>
        await dbContext.Companies.AddAsync(company, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class LocationRepository(OrganizationDbContext dbContext) : ILocationRepository
{
    public Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Locations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<PagedResult<Location>> GetPagedAsync(
        bool? isPublished,
        Guid? companyId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Locations.AsNoTracking();

        if (isPublished.HasValue)
        {
            query = query.Where(l => l.IsPublished == isPublished.Value);
        }

        if (companyId.HasValue)
        {
            query = query.Where(l => l.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.Name.ToLower().Contains(term) ||
                l.City.ToLower().Contains(term) ||
                l.Address.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(l => l.Name)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<Location>.Create(items, totalCount, pagedRequest.NormalizedPage, pagedRequest.NormalizedPageSize);
    }

    public Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return dbContext.Locations.AnyAsync(
            l => l.Slug.ToLower() == normalized && (!excludeId.HasValue || l.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task AddAsync(Location location, CancellationToken cancellationToken = default) =>
        await dbContext.Locations.AddAsync(location, cancellationToken);

    public void Remove(Location location) => dbContext.Locations.Remove(location);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ContractTypeRepository(OrganizationDbContext dbContext) : IContractTypeRepository
{
    public Task<ContractType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ContractTypes.FirstOrDefaultAsync(ct => ct.Id == id, cancellationToken);

    public async Task<PagedResult<ContractType>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ContractTypes.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(ct => ct.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(ct =>
                ct.Code.ToLower().Contains(term) ||
                ct.Label.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(ct => ct.IsSystem)
            .ThenBy(ct => ct.Code)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<ContractType>.Create(items, totalCount, pagedRequest.NormalizedPage, pagedRequest.NormalizedPageSize);
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = ContractType.NormalizeCode(code);
        return dbContext.ContractTypes.AnyAsync(
            ct => ct.Code == normalized && (!excludeId.HasValue || ct.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task AddAsync(ContractType contractType, CancellationToken cancellationToken = default) =>
        await dbContext.ContractTypes.AddAsync(contractType, cancellationToken);

    public void Remove(ContractType contractType) => dbContext.ContractTypes.Remove(contractType);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
