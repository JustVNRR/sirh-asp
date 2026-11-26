using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public sealed record EmployeeFilter(
    Guid? CompanyId,
    Guid? DirectManagerId,
    string? Department,
    bool? IsActive,
    bool? CurrentlyInPosition,
    IReadOnlyCollection<Guid>? UserIdsFilter,
    string? Search);

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Employee?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> IsManagerOfAsync(
        Guid managerEmployeeId,
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetDirectReportIdsAsync(
        Guid managerEmployeeId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<PagedResult<Employee>> GetPagedAsync(
        EmployeeFilter filter,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
