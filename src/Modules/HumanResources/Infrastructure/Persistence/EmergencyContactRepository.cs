using Microsoft.EntityFrameworkCore;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class EmergencyContactRepository(HumanResourcesDbContext dbContext) : IEmergencyContactRepository
{
    public Task<EmergencyContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.EmergencyContacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EmergencyContact>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        await dbContext.EmergencyContacts
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderBy(c => c.PriorityOrder)
            .ThenBy(c => c.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

    public Task<int> CountByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default) =>
        dbContext.EmergencyContacts.CountAsync(c => c.EmployeeId == employeeId, cancellationToken);

    public async Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken = default) =>
        await dbContext.EmergencyContacts.AddAsync(contact, cancellationToken);

    public void Remove(EmergencyContact contact) => dbContext.EmergencyContacts.Remove(contact);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
