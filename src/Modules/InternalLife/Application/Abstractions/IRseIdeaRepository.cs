using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface IRseIdeaRepository
{
    Task<RseIdea?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<RseIdea>> GetPagedAsync(
        Guid? authorEmployeeId,
        Guid? locationId,
        RseIdeaStatus? status,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task AddAsync(RseIdea idea, CancellationToken cancellationToken = default);

    void Remove(RseIdea idea);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
