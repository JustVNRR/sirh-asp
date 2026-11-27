using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.HumanResources.Domain.Entities;

public sealed class Employee : AuditableEntity
{
    private Employee()
    {
    }

    public Employee(
        Guid id,
        Guid userId,
        Guid companyId,
        Guid? directManagerId,
        string slug,
        string honorific,
        string firstName,
        string lastName,
        DateOnly? bornAt,
        string? birthPlace,
        string? birthDepartmentCode,
        string? nationalIdEncrypted,
        string email,
        string? landline,
        string? mobile,
        string? personalEmail,
        string? personalLandline,
        string? personalMobile,
        string? addressStreet,
        string? addressPostalCode,
        string? addressCity,
        string? addressAdditionalInfo,
        IEnumerable<string>? departments) : base(id)
    {
        UserId = userId;
        Slug = slug.Trim();
        IsActive = true;
        CurrentlyInPosition = false;
        ApplyMutableFields(
            companyId,
            directManagerId,
            honorific,
            firstName,
            lastName,
            bornAt,
            birthPlace,
            birthDepartmentCode,
            nationalIdEncrypted,
            email,
            landline,
            mobile,
            personalEmail,
            personalLandline,
            personalMobile,
            addressStreet,
            addressPostalCode,
            addressCity,
            addressAdditionalInfo,
            departments);
    }

    public Guid UserId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid? DirectManagerId { get; private set; }
    public Employee? DirectManager { get; init; }

    public string Slug { get; private set; } = string.Empty;
    public string Honorific { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;

    public DateOnly? BornAt { get; private set; }
    public string? BirthPlace { get; private set; }
    public string? BirthDepartmentCode { get; private set; }
    public string? NationalIdEncrypted { get; private set; }

    public string Email { get; private set; } = string.Empty;
    public string? Landline { get; private set; }
    public string? Mobile { get; private set; }

    public string? PersonalEmail { get; private set; }
    public string? PersonalLandline { get; private set; }
    public string? PersonalMobile { get; private set; }

    public string? AddressStreet { get; private set; }
    public string? AddressPostalCode { get; private set; }
    public string? AddressCity { get; private set; }
    public string? AddressAdditionalInfo { get; private set; }

    public string DepartmentsCsv { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
    public bool CurrentlyInPosition { get; private set; }

    public ICollection<EmergencyContact> EmergencyContacts { get; private set; } = [];
    public ICollection<Position> Positions { get; private set; } = [];
    public ICollection<EmployeeDocument> Documents { get; private set; } = [];

    public void Update(
        Guid companyId,
        Guid? directManagerId,
        string honorific,
        string firstName,
        string lastName,
        DateOnly? bornAt,
        string? birthPlace,
        string? birthDepartmentCode,
        string? nationalIdEncrypted,
        string email,
        string? landline,
        string? mobile,
        string? personalEmail,
        string? personalLandline,
        string? personalMobile,
        string? addressStreet,
        string? addressPostalCode,
        string? addressCity,
        string? addressAdditionalInfo,
        IEnumerable<string>? departments) =>
        ApplyMutableFields(
            companyId,
            directManagerId,
            honorific,
            firstName,
            lastName,
            bornAt,
            birthPlace,
            birthDepartmentCode,
            nationalIdEncrypted,
            email,
            landline,
            mobile,
            personalEmail,
            personalLandline,
            personalMobile,
            addressStreet,
            addressPostalCode,
            addressCity,
            addressAdditionalInfo,
            departments);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void SetCurrentlyInPosition(bool inPosition) => CurrentlyInPosition = inPosition;

    public void SetDirectManager(Guid? directManagerId) => DirectManagerId = directManagerId;

    public IReadOnlyList<string> GetDepartments() =>
        string.IsNullOrWhiteSpace(DepartmentsCsv)
            ? []
            : DepartmentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void ApplyMutableFields(
        Guid companyId,
        Guid? directManagerId,
        string honorific,
        string firstName,
        string lastName,
        DateOnly? bornAt,
        string? birthPlace,
        string? birthDepartmentCode,
        string? nationalIdEncrypted,
        string email,
        string? landline,
        string? mobile,
        string? personalEmail,
        string? personalLandline,
        string? personalMobile,
        string? addressStreet,
        string? addressPostalCode,
        string? addressCity,
        string? addressAdditionalInfo,
        IEnumerable<string>? departments)
    {
        CompanyId = companyId;
        DirectManagerId = directManagerId;
        Honorific = honorific.Trim();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        BornAt = bornAt;
        BirthPlace = NormalizeOptional(birthPlace);
        BirthDepartmentCode = NormalizeOptional(birthDepartmentCode);
        NationalIdEncrypted = NormalizeOptional(nationalIdEncrypted);
        Email = email.Trim().ToLowerInvariant();
        Landline = NormalizeOptional(landline);
        Mobile = NormalizeOptional(mobile);
        PersonalEmail = NormalizeOptional(personalEmail)?.ToLowerInvariant();
        PersonalLandline = NormalizeOptional(personalLandline);
        PersonalMobile = NormalizeOptional(personalMobile);
        AddressStreet = NormalizeOptional(addressStreet);
        AddressPostalCode = NormalizeOptional(addressPostalCode);
        AddressCity = NormalizeOptional(addressCity);
        AddressAdditionalInfo = NormalizeOptional(addressAdditionalInfo);
        DepartmentsCsv = departments is null
            ? string.Empty
            : string.Join(
                ',',
                departments
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Select(d => d.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(d => d));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
