namespace Sirh.Modules.Organization.Application.DTOs;

public sealed record CompanyDto(
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
    string? Owner,
    string? LogoUrl,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record CreateCompanyRequest(
    string Name,
    string? LegalForm,
    string Address,
    string? PostalCode,
    string? City,
    string? Country,
    string Siret,
    string? Register,
    string? Capital,
    string? CollectiveAgreement,
    string? Owner,
    string? LogoUrl);

public sealed record UpdateCompanyRequest(
    string Name,
    string? LegalForm,
    string Address,
    string? PostalCode,
    string? City,
    string? Country,
    string Siret,
    string? Register,
    string? Capital,
    string? CollectiveAgreement,
    string? Owner,
    string? LogoUrl,
    bool IsActive,
    string? RowVersion = null);

public sealed record UpdateCompanyStatusRequest(bool IsActive, string? RowVersion = null);

public sealed record LocationDto(
    Guid Id,
    string Slug,
    string Name,
    string Address,
    string? PostalCode,
    string City,
    string Country,
    string? DepartmentCode,
    string? Siret,
    string ColorCode,
    bool IsPublished,
    Guid? CompanyId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record CreateLocationRequest(
    string Name,
    string Address,
    string? PostalCode,
    string City,
    string? Country,
    string? DepartmentCode,
    string? Siret,
    string ColorCode,
    bool IsPublished = true,
    Guid? CompanyId = null);

public sealed record UpdateLocationRequest(
    string Name,
    string Address,
    string? PostalCode,
    string City,
    string? Country,
    string? DepartmentCode,
    string? Siret,
    string ColorCode,
    bool IsPublished,
    Guid? CompanyId = null,
    string? RowVersion = null);

public sealed record ContractTypeDto(
    Guid Id,
    string Code,
    string Label,
    bool IsSystem,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record CreateContractTypeRequest(
    string Code,
    string Label,
    bool IsActive = true);

public sealed record UpdateContractTypeRequest(
    string Code,
    string Label,
    bool IsActive,
    string? RowVersion = null);

public sealed record DepartmentDto(string Code, string Label);
