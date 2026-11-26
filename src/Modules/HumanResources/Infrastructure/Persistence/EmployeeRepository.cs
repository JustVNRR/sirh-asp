using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class EmployeeRepository(HumanResourcesDbContext dbContext) : IEmployeeRepository
{
    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Employee?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Employees.FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Employees.AnyAsync(e => e.Id == id, cancellationToken);

    public async Task<bool> IsManagerOfAsync(
        Guid managerEmployeeId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (managerEmployeeId == Guid.Empty || employeeId == Guid.Empty)
        {
            return false;
        }

        if (await dbContext.Employees.AsNoTracking()
                .AnyAsync(e => e.Id == employeeId && e.DirectManagerId == managerEmployeeId, cancellationToken))
        {
            return true;
        }

        return await dbContext.Positions.AsNoTracking()
            .AnyAsync(p => p.EmployeeId == employeeId && p.ManagerId == managerEmployeeId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetDirectReportIdsAsync(
        Guid managerEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var fromEmployee = await dbContext.Employees.AsNoTracking()
            .Where(e => e.DirectManagerId == managerEmployeeId)
            .Select(e => e.Id)
            .ToArrayAsync(cancellationToken);

        var fromPositions = await dbContext.Positions.AsNoTracking()
            .Where(p => p.ManagerId == managerEmployeeId)
            .Select(p => p.EmployeeId)
            .ToArrayAsync(cancellationToken);

        var result = new HashSet<Guid>(fromEmployee);
        result.UnionWith(fromPositions);
        return result;
    }

    public Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return dbContext.Employees.AnyAsync(
            e => e.Slug.ToLower() == normalized && (!excludeId.HasValue || e.Id != excludeId.Value),
            cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return dbContext.Employees.AnyAsync(
            e => e.Email.ToLower() == normalized && (!excludeId.HasValue || e.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task<PagedResult<Employee>> GetPagedAsync(
        EmployeeFilter filter,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Employees.AsNoTracking();

        if (filter.CompanyId.HasValue)
        {
            query = query.Where(e => e.CompanyId == filter.CompanyId.Value);
        }

        if (filter.DirectManagerId.HasValue)
        {
            query = query.Where(e => e.DirectManagerId == filter.DirectManagerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Department))
        {
            var dept = filter.Department.Trim().ToUpperInvariant();
            query = query.Where(e =>
                e.DepartmentsCsv == dept ||
                e.DepartmentsCsv.StartsWith(dept + ",") ||
                e.DepartmentsCsv.EndsWith("," + dept) ||
                e.DepartmentsCsv.Contains("," + dept + ","));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(e => e.IsActive == filter.IsActive.Value);
        }

        if (filter.CurrentlyInPosition.HasValue)
        {
            query = query.Where(e => e.CurrentlyInPosition == filter.CurrentlyInPosition.Value);
        }

        if (filter.UserIdsFilter is not null)
        {
            var allowedUserIds = filter.UserIdsFilter;
            query = query.Where(e => allowedUserIds.Contains(e.UserId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(term) ||
                e.LastName.ToLower().Contains(term) ||
                e.Email.ToLower().Contains(term) ||
                e.Slug.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<Employee>.Create(items, totalCount, pagedRequest.NormalizedPage, pagedRequest.NormalizedPageSize);
    }

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default) =>
        await dbContext.Employees.AddAsync(employee, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
