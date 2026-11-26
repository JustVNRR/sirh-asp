using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Contracts;

namespace Sirh.Modules.Contracts.Infrastructure.Persistence;

public sealed class ContractPositionReferenceChecker(ContractsDbContext dbContext) : IPositionReferenceChecker
{
    public async Task<bool> IsPositionReferencedAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Contracts.AsNoTracking().AnyAsync(c => c.PositionId == positionId, cancellationToken))
        {
            return true;
        }

        return await dbContext.JobSheets.AsNoTracking().AnyAsync(j => j.PositionId == positionId, cancellationToken);
    }
}

public sealed class ContractTypeUsageChecker(ContractsDbContext dbContext) : IContractTypeReferenceChecker
{
    public async Task<bool> IsContractTypeReferencedAsync(
        Guid contractTypeId,
        CancellationToken cancellationToken = default)
    {
        var usedByContract = await dbContext.Contracts
            .AsNoTracking()
            .AnyAsync(c => c.ContractTypeId == contractTypeId, cancellationToken);

        if (usedByContract)
        {
            return true;
        }

        return await dbContext.ContractModels
            .AsNoTracking()
            .AnyAsync(m => m.ContractTypeId == contractTypeId, cancellationToken);
    }
}
