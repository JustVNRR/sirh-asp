using Microsoft.EntityFrameworkCore;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class PositionRepository(HumanResourcesDbContext dbContext) : IPositionRepository
{
    public Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Positions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Position>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Positions
            .AsNoTracking()
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.StartAt)
            .ThenByDescending(p => p.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

    public Task<bool> ExistsBySlugAsync(
        string slug,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return dbContext.Positions.AnyAsync(
            p => p.Slug.ToLower() == normalized && (!excludeId.HasValue || p.Id != excludeId.Value),
            cancellationToken);
    }

    public Task<bool> IsManagerOfEmployeeAsync(
        Guid managerEmployeeId,
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        dbContext.Positions
            .AsNoTracking()
            .AnyAsync(p => p.EmployeeId == employeeId && p.ManagerId == managerEmployeeId, cancellationToken);

    public async Task AddAsync(Position position, CancellationToken cancellationToken = default) =>
        await dbContext.Positions.AddAsync(position, cancellationToken);

    public void Remove(Position position) => dbContext.Positions.Remove(position);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
