using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.HumanResources.Domain.Entities;

public sealed class EmergencyContact : AuditableEntity
{
    public const int MaxContactsPerEmployee = 2;

    private EmergencyContact()
    {
    }

    public EmergencyContact(
        Guid employeeId,
        string fullNameEncrypted,
        string relationshipEncrypted,
        string? mobilePhoneEncrypted,
        string? homePhoneEncrypted,
        string? workPhoneEncrypted,
        int priorityOrder)
    {
        EmployeeId = employeeId;
        ApplyFields(
            fullNameEncrypted,
            relationshipEncrypted,
            mobilePhoneEncrypted,
            homePhoneEncrypted,
            workPhoneEncrypted,
            priorityOrder);
    }

    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;

    public string FullNameEncrypted { get; private set; } = string.Empty;
    public string RelationshipEncrypted { get; private set; } = string.Empty;
    public string? MobilePhoneEncrypted { get; private set; }
    public string? HomePhoneEncrypted { get; private set; }
    public string? WorkPhoneEncrypted { get; private set; }
    public int PriorityOrder { get; private set; }

    public void Update(
        string fullNameEncrypted,
        string relationshipEncrypted,
        string? mobilePhoneEncrypted,
        string? homePhoneEncrypted,
        string? workPhoneEncrypted,
        int priorityOrder) =>
        ApplyFields(
            fullNameEncrypted,
            relationshipEncrypted,
            mobilePhoneEncrypted,
            homePhoneEncrypted,
            workPhoneEncrypted,
            priorityOrder);

    private void ApplyFields(
        string fullNameEncrypted,
        string relationshipEncrypted,
        string? mobilePhoneEncrypted,
        string? homePhoneEncrypted,
        string? workPhoneEncrypted,
        int priorityOrder)
    {
        FullNameEncrypted = fullNameEncrypted;
        RelationshipEncrypted = relationshipEncrypted;
        MobilePhoneEncrypted = mobilePhoneEncrypted;
        HomePhoneEncrypted = homePhoneEncrypted;
        WorkPhoneEncrypted = workPhoneEncrypted;
        PriorityOrder = priorityOrder;
    }
}
