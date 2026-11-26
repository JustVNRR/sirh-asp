using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Contracts;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class RseIdeaLocationReferenceChecker(InternalLifeDbContext dbContext)
    : ILocationReferenceChecker
{
    public Task<bool> IsLocationReferencedAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        dbContext.RseIdeas
            .AsNoTracking()
            .AnyAsync(i => i.LocationId == locationId, cancellationToken);
}
