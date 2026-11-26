using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Application.Abstractions;

public interface IMedicalVisitRepository
{
    Task<MedicalVisit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<MedicalVisit>> GetPagedAsync(
        Guid? employeeId,
        MedicalVisitType? visitType,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        bool? carriedOut,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task AddAsync(MedicalVisit visit, CancellationToken cancellationToken = default);

    void Remove(MedicalVisit visit);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
