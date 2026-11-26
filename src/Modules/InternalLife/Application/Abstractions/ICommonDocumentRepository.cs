using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface ICommonDocumentRepository
{
    Task<CommonDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<CommonDocument>> GetPagedAsync(
        Guid? companyId,
        CommonDocumentCategory? category,
        bool? isWelcomeKit,
        Guid? contractTypeId,
        bool? archived,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task AddAsync(CommonDocument document, CancellationToken cancellationToken = default);

    void Remove(CommonDocument document);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
