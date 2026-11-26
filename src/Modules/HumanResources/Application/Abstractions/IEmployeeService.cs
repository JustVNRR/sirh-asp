using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.DTOs;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public interface IEmployeeService
{
    Task<Result<PagedResult<EmployeeSummaryDto>>> GetPagedAsync(
        EmployeeQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
