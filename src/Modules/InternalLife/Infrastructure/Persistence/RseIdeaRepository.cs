using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class RseIdeaRepository(InternalLifeDbContext dbContext) : IRseIdeaRepository
{
    public Task<RseIdea?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.RseIdeas.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<PagedResult<RseIdea>> GetPagedAsync(
        Guid? authorEmployeeId,
        Guid? locationId,
        RseIdeaStatus? status,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.RseIdeas.AsNoTracking();

        if (authorEmployeeId.HasValue)
        {
            query = query.Where(i => i.AuthorEmployeeId == authorEmployeeId.Value);
        }

        if (locationId.HasValue)
        {
            query = query.Where(i => i.LocationId == locationId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(i =>
                i.Title.ToLower().Contains(term) ||
                i.IdeaContent.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<RseIdea>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public async Task AddAsync(RseIdea idea, CancellationToken cancellationToken = default) =>
        await dbContext.RseIdeas.AddAsync(idea, cancellationToken);

    public void Remove(RseIdea idea) => dbContext.RseIdeas.Remove(idea);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
