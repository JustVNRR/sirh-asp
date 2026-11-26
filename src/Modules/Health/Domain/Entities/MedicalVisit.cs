using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Domain.Entities;

public sealed class MedicalVisit : AuditableEntity
{
    private MedicalVisit()
    {
    }

    public MedicalVisit(
        Guid employeeId,
        MedicalVisitType visitType,
        DateTime visitDateTimeUtc,
        DateOnly? validityDate,
        string? medicalCenter,
        bool carriedOut,
        string? comments,
        string convocationFileKey,
        string convocationFileName,
        string convocationContentType,
        long convocationFileSizeBytes)
    {
        EmployeeId = employeeId;
        VisitType = visitType;
        VisitDateTimeUtc = NormalizeUtc(visitDateTimeUtc);
        ValidityDate = validityDate;
        MedicalCenter = NormalizeText(medicalCenter);
        CarriedOut = carriedOut;
        Comments = NormalizeText(comments);
        ConvocationFileKey = convocationFileKey;
        ConvocationFileName = convocationFileName;
        ConvocationContentType = convocationContentType;
        ConvocationFileSizeBytes = convocationFileSizeBytes;
    }

    public Guid EmployeeId { get; private set; }
    public MedicalVisitType VisitType { get; private set; }
    public DateTime VisitDateTimeUtc { get; private set; }
    public DateOnly? ValidityDate { get; private set; }
    public string? MedicalCenter { get; private set; }
    public bool CarriedOut { get; private set; }
    public string? Comments { get; private set; }

    public string ConvocationFileKey { get; private set; } = string.Empty;
    public string ConvocationFileName { get; private set; } = string.Empty;
    public string ConvocationContentType { get; private set; } = string.Empty;
    public long ConvocationFileSizeBytes { get; private set; }

    public string? ReportFileKey { get; private set; }
    public string? ReportFileName { get; private set; }
    public string? ReportContentType { get; private set; }
    public long? ReportFileSizeBytes { get; private set; }

    public DateTime? ReminderSentAtUtc { get; private set; }

    public void UpdateMetadata(
        MedicalVisitType visitType,
        DateTime visitDateTimeUtc,
        DateOnly? validityDate,
        string? medicalCenter,
        bool carriedOut,
        string? comments)
    {
        var normalizedDateTimeUtc = NormalizeUtc(visitDateTimeUtc);
        if (VisitDateTimeUtc != normalizedDateTimeUtc)
        {
            ReminderSentAtUtc = null;
        }

        VisitType = visitType;
        VisitDateTimeUtc = normalizedDateTimeUtc;
        ValidityDate = validityDate;
        MedicalCenter = NormalizeText(medicalCenter);
        CarriedOut = carriedOut;
        Comments = NormalizeText(comments);
    }

    public void ReplaceConvocationFile(
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes)
    {
        ConvocationFileKey = fileKey;
        ConvocationFileName = fileName;
        ConvocationContentType = contentType;
        ConvocationFileSizeBytes = fileSizeBytes;
    }

    public void SetReportFile(
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes)
    {
        ReportFileKey = fileKey;
        ReportFileName = fileName;
        ReportContentType = contentType;
        ReportFileSizeBytes = fileSizeBytes;
    }

    public void MarkReminderSent(DateTime sentAtUtc)
    {
        ReminderSentAtUtc = NormalizeUtc(sentAtUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
