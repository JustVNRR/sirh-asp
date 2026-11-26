using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Contracts;

namespace Sirh.Modules.Organization.Infrastructure.Persistence;

public sealed class OrganizationLookupService(OrganizationDbContext dbContext) : IOrganizationLookup
{
    public Task<CompanyLookupDto?> GetCompanyByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new CompanyLookupDto(c.Id, c.Name, c.Slug, c.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> LocationExistsAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        dbContext.Locations
            .AsNoTracking()
            .AnyAsync(l => l.Id == locationId, cancellationToken);
}
