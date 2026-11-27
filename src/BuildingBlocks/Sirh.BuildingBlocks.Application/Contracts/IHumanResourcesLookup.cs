namespace Sirh.BuildingBlocks.Application.Contracts;

public sealed record PositionContractContextDto(
    Guid PositionId,
    string PositionSlug,
    Guid EmployeeId,
    Guid? ManagerId,
    Guid CompanyId,
    Guid? LocationId,
    string JobTitle,
    DateOnly StartAt,
    DateOnly? EndAt,
    string? ContractLabel,
    string? Coefficient,
    decimal? Rate,
    string? Supervisor,
    string? TimeContractType,
    decimal? TimeContractAmount,
    string? HierarchicalPosition,
    string EmployeeFirstName,
    string EmployeeLastName,
    string EmployeeHonorific,
    string EmployeeEmail,
    DateOnly? EmployeeBornAt,
    string? EmployeeBirthPlace,
    string? EmployeeAddressStreet,
    string? EmployeeAddressPostalCode,
    string? EmployeeAddressCity,
    string? EmployeeNationalId);

public sealed record EmployeeContactDto(
    Guid EmployeeId,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive);

public sealed record ExpiringDocumentAlertDto(
    Guid DocumentId,
    Guid EmployeeId,
    string EmployeeFirstName,
    string EmployeeLastName,
    string EmployeeEmail,
    string DocumentType,
    DateOnly ExpiryDate,
    string? ReferenceNumber,
    DateTime? ExpiryReminder30SentAtUtc,
    DateTime? ExpiryReminder7SentAtUtc);

public interface IHumanResourcesLookup
{
    Task<PositionContractContextDto?> GetPositionContractContextAsync(
        Guid positionId,
        CancellationToken cancellationToken = default);

    Task<bool> IsManagerOfEmployeeAsync(
        Guid managerEmployeeId,
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<bool> EmployeeExistsAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, EmployeeContactDto>> GetEmployeeContactsByIdsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeContactDto>> GetActiveEmployeesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpiringDocumentAlertDto>> GetExpiringDocumentsAsync(
        DateOnly todayUtc,
        int maxDaysAhead,
        CancellationToken cancellationToken = default);

    Task MarkDocumentExpiryReminderSentAsync(
        Guid documentId,
        string documentType,
        int thresholdDays,
        DateTime sentAtUtc,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetEmployeeCompanyIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);
}
