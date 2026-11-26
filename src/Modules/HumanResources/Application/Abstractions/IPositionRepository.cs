using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public interface IPositionRepository
{
    Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Position>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsBySlugAsync(
        string slug,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsManagerOfEmployeeAsync(
        Guid managerEmployeeId,
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Position position, CancellationToken cancellationToken = default);

    void Remove(Position position);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
