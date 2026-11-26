using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Contracts.Domain.Entities;

namespace Sirh.Modules.Contracts.Application.Abstractions;

public interface IContractModelRepository
{
    Task<ContractModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<ContractModel>> GetPagedAsync(
        bool? isActive,
        Guid? contractTypeId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByTitleAsync(string title, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(ContractModel model, CancellationToken cancellationToken = default);

    void Remove(ContractModel model);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Contract?> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);

    Task AddAsync(Contract contract, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
