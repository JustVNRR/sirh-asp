using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class StaffRepresentativeRepository(InternalLifeDbContext dbContext) : IStaffRepresentativeRepository
{
    public Task<StaffRepresentative?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.StaffRepresentatives.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<PagedResult<StaffRepresentative>> GetPagedAsync(
        Guid? companyId,
        StaffRepresentativeRole? role,
        bool? harassmentAdvisor,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.StaffRepresentatives.AsNoTracking();

        if (companyId.HasValue)
        {
            query = query.Where(r => r.CompanyId == companyId.Value);
        }

        if (role.HasValue)
        {
            query = query.Where(r => r.RepresentativeRole == role.Value);
        }

        if (harassmentAdvisor.HasValue)
        {
            query = query.Where(r => r.IsHarassmentAdvisor == harassmentAdvisor.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.FirstName.ToLower().Contains(term) ||
                r.LastName.ToLower().Contains(term) ||
                r.Email.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.LastName)
            .ThenBy(r => r.FirstName)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<StaffRepresentative>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public Task<bool> ExistsByEmailAsync(
        Guid companyId,
        string email,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return dbContext.StaffRepresentatives.AnyAsync(
            r => r.CompanyId == companyId &&
                 r.Email == normalized &&
                 (!excludeId.HasValue || r.Id != excludeId.Value),
            cancellationToken);
    }

    public Task<bool> ExistsByEmployeeIdAsync(
        Guid companyId,
        Guid employeeId,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        dbContext.StaffRepresentatives.AnyAsync(
            r => r.CompanyId == companyId &&
                 r.EmployeeId == employeeId &&
                 (!excludeId.HasValue || r.Id != excludeId.Value),
            cancellationToken);

    public async Task AddAsync(StaffRepresentative representative, CancellationToken cancellationToken = default) =>
        await dbContext.StaffRepresentatives.AddAsync(representative, cancellationToken);

    public void Remove(StaffRepresentative representative) => dbContext.StaffRepresentatives.Remove(representative);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
