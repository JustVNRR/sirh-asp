namespace Sirh.BuildingBlocks.Application.Contracts;

public sealed record CompanyLookupDto(Guid Id, string Name, string Slug, bool IsActive);

public sealed record CompanyContractDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string? LegalForm,
    string Address,
    string? PostalCode,
    string? City,
    string Country,
    string Siret,
    string? Register,
    string? Capital,
    string CollectiveAgreement,
    string? Owner);

public interface IOrganizationLookup
{
    Task<CompanyLookupDto?> GetCompanyByIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<CompanyContractDetailsDto?> GetCompanyDetailsByIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<bool> LocationExistsAsync(Guid locationId, CancellationToken cancellationToken = default);

    Task<string?> GetLocationNameByIdAsync(Guid locationId, CancellationToken cancellationToken = default);

    Task<bool> ContractTypeExistsAsync(Guid contractTypeId, CancellationToken cancellationToken = default);
}
