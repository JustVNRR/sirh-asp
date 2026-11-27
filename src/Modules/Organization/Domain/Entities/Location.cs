using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.Organization.Domain.Entities;

public sealed class Location : AuditableEntity
{
    public const string DefaultCountry = "France";

    private Location()
    {
    }

    public Location(
        string slug,
        string name,
        string address,
        string? postalCode,
        string city,
        string? country,
        string? departmentCode,
        string? siret,
        string colorCode,
        bool isPublished,
        Guid? companyId)
    {
        ApplyFields(
            slug,
            name,
            address,
            postalCode,
            city,
            country,
            departmentCode,
            siret,
            colorCode,
            isPublished,
            companyId);
    }

    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string? PostalCode { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string Country { get; private set; } = DefaultCountry;
    public string? DepartmentCode { get; private set; }
    public string? Siret { get; private set; }
    public string ColorCode { get; private set; } = string.Empty;
    public bool IsPublished { get; private set; }

    public Guid? CompanyId { get; private set; }
    public Company? Company { get; init; }

    public void Update(
        string slug,
        string name,
        string address,
        string? postalCode,
        string city,
        string? country,
        string? departmentCode,
        string? siret,
        string colorCode,
        bool isPublished,
        Guid? companyId)
    {
        ApplyFields(
            slug,
            name,
            address,
            postalCode,
            city,
            country,
            departmentCode,
            siret,
            colorCode,
            isPublished,
            companyId);
    }

    private void ApplyFields(
        string slug,
        string name,
        string address,
        string? postalCode,
        string city,
        string? country,
        string? departmentCode,
        string? siret,
        string colorCode,
        bool isPublished,
        Guid? companyId)
    {
        Slug = slug.Trim();
        Name = name.Trim();
        Address = address.Trim();
        PostalCode = NormalizeOptional(postalCode);
        City = city.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? DefaultCountry : country.Trim();
        DepartmentCode = NormalizeOptional(departmentCode);
        Siret = NormalizeOptional(siret);
        ColorCode = colorCode.Trim().ToUpperInvariant();
        IsPublished = isPublished;
        CompanyId = companyId;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
