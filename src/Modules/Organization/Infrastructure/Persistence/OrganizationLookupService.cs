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

    public Task<CompanyContractDetailsDto?> GetCompanyDetailsByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new CompanyContractDetailsDto(
                c.Id,
                c.Name,
                c.Slug,
                c.LegalForm,
                c.Address,
                c.PostalCode,
                c.City,
                c.Country,
                c.Siret,
                c.Register,
                c.Capital,
                c.CollectiveAgreement,
                c.Owner))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> LocationExistsAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        dbContext.Locations
            .AsNoTracking()
            .AnyAsync(l => l.Id == locationId, cancellationToken);

    public Task<string?> GetLocationNameByIdAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        dbContext.Locations
            .AsNoTracking()
            .Where(l => l.Id == locationId)
            .Select(l => (string?)l.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ContractTypeExistsAsync(Guid contractTypeId, CancellationToken cancellationToken = default) =>
        dbContext.ContractTypes
            .AsNoTracking()
            .AnyAsync(ct => ct.Id == contractTypeId, cancellationToken);

    public Task<Guid?> GetCompanyIdBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> GetLocationIdBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        dbContext.Locations
            .AsNoTracking()
            .Where(l => l.Slug == slug)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
