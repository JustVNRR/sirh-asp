namespace Sirh.Modules.HumanResources.Application.DTOs;

public sealed record EmergencyContactDto(
    Guid Id,
    Guid EmployeeId,
    string FullName,
    string Relationship,
    string? MobilePhone,
    string? HomePhone,
    string? WorkPhone,
    int PriorityOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record CreateEmergencyContactRequest(
    string FullName,
    string Relationship,
    string? MobilePhone = null,
    string? HomePhone = null,
    string? WorkPhone = null,
    int PriorityOrder = 1);

public sealed record UpdateEmergencyContactRequest(
    string FullName,
    string Relationship,
    string? MobilePhone = null,
    string? HomePhone = null,
    string? WorkPhone = null,
    int PriorityOrder = 1,
    string? RowVersion = null);
