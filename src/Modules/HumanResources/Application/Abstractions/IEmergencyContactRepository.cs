using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public interface IEmergencyContactRepository
{
    Task<EmergencyContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmergencyContact>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<int> CountByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken = default);

    void Remove(EmergencyContact contact);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
