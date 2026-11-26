using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.Organization.Domain.Entities;

public sealed class Company : AuditableEntity
{
    public const string DefaultCollectiveAgreement = "Syntec";
    public const string DefaultCountry = "France";

    private Company()
    {
    }

    public Company(
        string name,
        string slug,
        string? legalForm,
        string address,
        string? postalCode,
        string? city,
        string? country,
        string siret,
        string? register,
        string? capital,
        string? collectiveAgreement,
        string? owner,
        string? logoUrl)
    {
        ApplyFields(
            name,
            slug,
            legalForm,
            address,
            postalCode,
            city,
            country,
            siret,
            register,
            capital,
            collectiveAgreement,
            owner,
            logoUrl);
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? LegalForm { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string? PostalCode { get; private set; }
    public string? City { get; private set; }
    public string Country { get; private set; } = DefaultCountry;
    public string Siret { get; private set; } = string.Empty;
    public string? Register { get; private set; }
    public string? Capital { get; private set; }
    public string CollectiveAgreement { get; private set; } = DefaultCollectiveAgreement;
    public string? Owner { get; private set; }
    public string? LogoUrl { get; private set; }
    public bool IsActive { get; private set; }

    public ICollection<Location> Locations { get; private set; } = [];

    public void Update(
        string name,
        string slug,
        string? legalForm,
        string address,
        string? postalCode,
        string? city,
        string? country,
        string siret,
        string? register,
        string? capital,
        string? collectiveAgreement,
        string? owner,
        string? logoUrl,
        bool isActive)
    {
        ApplyFields(
            name,
            slug,
            legalForm,
            address,
            postalCode,
            city,
            country,
            siret,
            register,
            capital,
            collectiveAgreement,
            owner,
            logoUrl);
        IsActive = isActive;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private void ApplyFields(
        string name,
        string slug,
        string? legalForm,
        string address,
        string? postalCode,
        string? city,
        string? country,
        string siret,
        string? register,
        string? capital,
        string? collectiveAgreement,
        string? owner,
        string? logoUrl)
    {
        Name = name.Trim();
        Slug = slug.Trim();
        LegalForm = NormalizeOptional(legalForm);
        Address = address.Trim();
        PostalCode = NormalizeOptional(postalCode);
        City = NormalizeOptional(city);
        Country = string.IsNullOrWhiteSpace(country) ? DefaultCountry : country.Trim();
        Siret = siret.Trim();
        Register = NormalizeOptional(register);
        Capital = NormalizeOptional(capital);
        CollectiveAgreement = string.IsNullOrWhiteSpace(collectiveAgreement)
            ? DefaultCollectiveAgreement
            : collectiveAgreement.Trim();
        Owner = NormalizeOptional(owner);
        LogoUrl = NormalizeOptional(logoUrl);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
