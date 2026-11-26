using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface IStaffRepresentativeRepository
{
    Task<StaffRepresentative?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<StaffRepresentative>> GetPagedAsync(
        Guid? companyId,
        StaffRepresentativeRole? role,
        bool? harassmentAdvisor,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(
        Guid companyId,
        string email,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmployeeIdAsync(
        Guid companyId,
        Guid employeeId,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(StaffRepresentative representative, CancellationToken cancellationToken = default);

    void Remove(StaffRepresentative representative);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
