using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Application.DTOs;

public sealed record MedicalVisitDto(
    Guid Id,
    Guid EmployeeId,
    MedicalVisitType VisitType,
    DateTime VisitDateTimeUtc,
    DateOnly? ValidityDate,
    string? MedicalCenter,
    bool CarriedOut,
    string? Comments,
    string ConvocationFileName,
    string ConvocationContentType,
    long ConvocationFileSizeBytes,
    bool HasReport,
    string? ReportFileName,
    string? ReportContentType,
    long? ReportFileSizeBytes,
    DateTime? ReminderSentAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record DownloadedHealthFileDto(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes);

public sealed record UploadedHealthFileInput(
    Stream Content,
    string FileName,
    string ContentType);

public sealed record MedicalVisitQuery(
    Guid? EmployeeId,
    MedicalVisitType? VisitType,
    DateTime? FromDateUtc,
    DateTime? ToDateUtc,
    bool? CarriedOut);

public sealed record CreateMedicalVisitCommand(
    Guid EmployeeId,
    MedicalVisitType VisitType,
    DateTime VisitDateTimeUtc,
    DateOnly? ValidityDate,
    string? MedicalCenter,
    bool CarriedOut,
    string? Comments,
    UploadedHealthFileInput ConvocationFile,
    UploadedHealthFileInput? ReportFile);

public sealed record UpdateMedicalVisitCommand(
    MedicalVisitType VisitType,
    DateTime VisitDateTimeUtc,
    DateOnly? ValidityDate,
    string? MedicalCenter,
    bool CarriedOut,
    string? Comments,
    UploadedHealthFileInput? ConvocationFile,
    UploadedHealthFileInput? ReportFile,
    string? RowVersion);
