using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Domain.Entities;
using Sirh.Modules.HumanResources.Domain.Enums;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class EmployeeDocumentRepository(HumanResourcesDbContext dbContext) : IEmployeeDocumentRepository
{
    public Task<EmployeeDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.EmployeeDocuments
            .Include(d => d.RqthDetail)
            .Include(d => d.ResidentPermitDetail)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<PagedResult<EmployeeDocument>> GetPagedAsync(
        IReadOnlyCollection<Guid>? allowedEmployeeIds,
        bool excludeSensitive,
        EmployeeDocumentCategory? category,
        IReadOnlyCollection<EmployeeDocumentType>? documentTypes,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmployeeDocuments
            .AsNoTracking()
            .Include(d => d.RqthDetail)
            .Include(d => d.ResidentPermitDetail)
            .AsQueryable();

        if (allowedEmployeeIds is not null)
        {
            var allowed = allowedEmployeeIds;
            query = query.Where(d => allowed.Contains(d.EmployeeId));
        }

        if (excludeSensitive)
        {
            query = query.Where(d =>
                d.Category != EmployeeDocumentCategory.Disciplinary &&
                d.Category != EmployeeDocumentCategory.Departure);
        }

        if (category.HasValue)
        {
            query = query.Where(d => d.Category == category.Value);
        }

        if (documentTypes is { Count: > 0 })
        {
            var types = documentTypes;
            query = query.Where(d => types.Contains(d.DocumentType));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(d =>
                d.FileName.ToLower().Contains(term) ||
                (d.Commentary != null && d.Commentary.ToLower().Contains(term)) ||
                (d.ResidentPermitDetail != null && d.ResidentPermitDetail.PermitNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.FileDate)
            .ThenByDescending(d => d.CreatedAtUtc)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<EmployeeDocument>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public async Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken = default) =>
        await dbContext.EmployeeDocuments.AddAsync(document, cancellationToken);

    public void Remove(EmployeeDocument document) => dbContext.EmployeeDocuments.Remove(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
