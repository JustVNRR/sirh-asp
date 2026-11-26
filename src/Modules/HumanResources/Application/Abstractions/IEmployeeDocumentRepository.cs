using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Domain.Entities;
using Sirh.Modules.HumanResources.Domain.Enums;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public interface IEmployeeDocumentRepository
{
    Task<EmployeeDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<EmployeeDocument>> GetPagedAsync(
        IReadOnlyCollection<Guid>? allowedEmployeeIds,
        bool excludeSensitive,
        EmployeeDocumentCategory? category,
        IReadOnlyCollection<EmployeeDocumentType>? documentTypes,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken = default);

    void Remove(EmployeeDocument document);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
