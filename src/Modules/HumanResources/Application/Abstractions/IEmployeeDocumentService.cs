using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.DTOs;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public interface IEmployeeDocumentService
{
    Task<Result<PagedResult<EmployeeDocumentDto>>> GetPagedAsync(
        EmployeeDocumentQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDocumentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DownloadedEmployeeDocumentDto>> OpenFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<EmployeeDocumentDto>> CreateAsync(
        CreateEmployeeDocumentCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDocumentDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeDocumentCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
