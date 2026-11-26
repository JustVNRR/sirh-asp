using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Domain.Entities;

namespace Sirh.Modules.Contracts.Infrastructure.Persistence;

public sealed class ContractModelRepository(ContractsDbContext dbContext) : IContractModelRepository
{
    public Task<ContractModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ContractModels.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<PagedResult<ContractModel>> GetPagedAsync(
        bool? isActive,
        Guid? contractTypeId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ContractModels.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        if (contractTypeId.HasValue)
        {
            query = query.Where(m => m.ContractTypeId == contractTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.Title.ToLower().Contains(term) ||
                (m.Description != null && m.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(m => m.Title)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<ContractModel>.Create(items, totalCount, pagedRequest.NormalizedPage, pagedRequest.NormalizedPageSize);
    }

    public Task<bool> ExistsByTitleAsync(string title, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = title.Trim().ToLowerInvariant();
        return dbContext.ContractModels.AnyAsync(
            m => m.Title.ToLower() == normalized && (!excludeId.HasValue || m.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task AddAsync(ContractModel model, CancellationToken cancellationToken = default) =>
        await dbContext.ContractModels.AddAsync(model, cancellationToken);

    public void Remove(ContractModel model) => dbContext.ContractModels.Remove(model);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ContractRepository(ContractsDbContext dbContext) : IContractRepository
{
    public Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Contract?> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default) =>
        dbContext.Contracts.FirstOrDefaultAsync(c => c.PositionId == positionId, cancellationToken);

    public Task<bool> ExistsByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default) =>
        dbContext.Contracts.AnyAsync(c => c.PositionId == positionId, cancellationToken);

    public async Task AddAsync(Contract contract, CancellationToken cancellationToken = default) =>
        await dbContext.Contracts.AddAsync(contract, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
