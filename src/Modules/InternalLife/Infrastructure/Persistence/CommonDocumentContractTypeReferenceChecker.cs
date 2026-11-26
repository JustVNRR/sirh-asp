using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Contracts;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class CommonDocumentContractTypeReferenceChecker(InternalLifeDbContext dbContext)
    : IContractTypeReferenceChecker
{
    public Task<bool> IsContractTypeReferencedAsync(
        Guid contractTypeId,
        CancellationToken cancellationToken = default) =>
        dbContext.CommonDocuments
            .AsNoTracking()
            .AnyAsync(d => d.ContractTypeId == contractTypeId, cancellationToken);
}
