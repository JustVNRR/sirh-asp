using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Contracts;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class PositionLocationReferenceChecker(HumanResourcesDbContext dbContext) : ILocationReferenceChecker
{
    public Task<bool> IsLocationReferencedAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        dbContext.Positions
            .AsNoTracking()
            .AnyAsync(p => p.LocationId == locationId, cancellationToken);
}
