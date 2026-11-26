using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Health.Application.DTOs;

namespace Sirh.Modules.Health.Application.Abstractions;

public interface IMedicalVisitService
{
    Task<Result<PagedResult<MedicalVisitDto>>> GetPagedAsync(
        MedicalVisitQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<MedicalVisitDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DownloadedHealthFileDto>> OpenConvocationFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DownloadedHealthFileDto>> OpenReportFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<MedicalVisitDto>> CreateAsync(
        CreateMedicalVisitCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<MedicalVisitDto>> UpdateAsync(
        Guid id,
        UpdateMedicalVisitCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
