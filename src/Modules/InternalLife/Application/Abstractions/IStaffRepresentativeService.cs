using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface IStaffRepresentativeService
{
    Task<Result<PagedResult<StaffRepresentativeDto>>> GetPagedAsync(
        Guid? companyId,
        StaffRepresentativeRole? role,
        bool? harassmentAdvisor,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<StaffRepresentativeDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<StaffRepresentativeDto>> CreateAsync(
        CreateStaffRepresentativeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StaffRepresentativeDto>> UpdateAsync(
        Guid id,
        UpdateStaffRepresentativeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
