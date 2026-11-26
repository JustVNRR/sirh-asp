using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class CommonDocumentRepository(InternalLifeDbContext dbContext) : ICommonDocumentRepository
{
    public Task<CommonDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.CommonDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<PagedResult<CommonDocument>> GetPagedAsync(
        Guid? companyId,
        CommonDocumentCategory? category,
        bool? isWelcomeKit,
        Guid? contractTypeId,
        bool? archived,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.CommonDocuments.AsNoTracking();

        if (companyId.HasValue)
        {
            query = query.Where(d => d.CompanyId == null || d.CompanyId == companyId.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(d => d.Category == category.Value);
        }

        if (isWelcomeKit.HasValue)
        {
            query = query.Where(d => d.IsWelcomeKit == isWelcomeKit.Value);
        }

        if (contractTypeId.HasValue)
        {
            query = query.Where(d => d.ContractTypeId == null || d.ContractTypeId == contractTypeId.Value);
        }

        if (archived.HasValue)
        {
            query = query.Where(d => d.IsArchived == archived.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(d =>
                d.Title.ToLower().Contains(term) ||
                (d.Description != null && d.Description.ToLower().Contains(term)) ||
                d.FileName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.FileDate)
            .ThenBy(d => d.Title)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<CommonDocument>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public async Task AddAsync(CommonDocument document, CancellationToken cancellationToken = default) =>
        await dbContext.CommonDocuments.AddAsync(document, cancellationToken);

    public void Remove(CommonDocument document) => dbContext.CommonDocuments.Remove(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
