namespace Sirh.BuildingBlocks.Application.Contracts;

public sealed record CompanyLookupDto(Guid Id, string Name, string Slug, bool IsActive);

public interface IOrganizationLookup
{
    Task<CompanyLookupDto?> GetCompanyByIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<bool> LocationExistsAsync(Guid locationId, CancellationToken cancellationToken = default);
}
