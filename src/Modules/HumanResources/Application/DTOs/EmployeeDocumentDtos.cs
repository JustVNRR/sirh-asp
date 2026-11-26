using Sirh.Modules.HumanResources.Domain.Enums;

namespace Sirh.Modules.HumanResources.Application.DTOs;

public sealed record RqthDetailDto(
    DateOnly ExpiryDate,
    string? WorkplaceAccommodation,
    DateTime? ExpiryReminder30SentAtUtc,
    DateTime? ExpiryReminder7SentAtUtc);

public sealed record ResidentPermitDetailDto(
    string PermitNumber,
    DateOnly ExpiryDate,
    DateTime? ExpiryReminder30SentAtUtc,
    DateTime? ExpiryReminder7SentAtUtc);

public sealed record EmployeeDocumentDto(
    Guid Id,
    Guid EmployeeId,
    EmployeeDocumentCategory Category,
    EmployeeDocumentType DocumentType,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateOnly FileDate,
    string? Commentary,
    RqthDetailDto? RqthDetails,
    ResidentPermitDetailDto? ResidentPermitDetails,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record DownloadedEmployeeDocumentDto(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes);

public sealed record UploadedDocumentFileInput(
    Stream Content,
    string FileName,
    string ContentType);

public sealed record EmployeeDocumentQuery(
    Guid? EmployeeId,
    EmployeeDocumentCategory? Category,
    IReadOnlyCollection<EmployeeDocumentType>? DocumentTypes,
    string? Search);

public sealed record CreateEmployeeDocumentCommand(
    Guid? EmployeeId,
    EmployeeDocumentCategory? Category,
    EmployeeDocumentType DocumentType,
    DateOnly? FileDate,
    string? Commentary,
    DateOnly? ExpiryDate,
    string? WorkplaceAccommodation,
    string? PermitNumber,
    UploadedDocumentFileInput File);

public sealed record UpdateEmployeeDocumentCommand(
    EmployeeDocumentCategory? Category,
    EmployeeDocumentType DocumentType,
    DateOnly? FileDate,
    string? Commentary,
    DateOnly? ExpiryDate,
    string? WorkplaceAccommodation,
    string? PermitNumber,
    UploadedDocumentFileInput? File,
    string? RowVersion);
