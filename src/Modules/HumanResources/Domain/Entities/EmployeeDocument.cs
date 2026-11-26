using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.HumanResources.Domain.Constants;
using Sirh.Modules.HumanResources.Domain.Enums;

namespace Sirh.Modules.HumanResources.Domain.Entities;

public sealed class EmployeeDocument : AuditableEntity
{
    private EmployeeDocument()
    {
    }

    public EmployeeDocument(
        Guid employeeId,
        EmployeeDocumentType documentType,
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        DateOnly fileDate,
        string? commentary)
    {
        EmployeeId = employeeId;
        DocumentType = documentType;
        Category = EmployeeDocumentTaxonomy.GetCategory(documentType);
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        FileDate = fileDate;
        Commentary = string.IsNullOrWhiteSpace(commentary) ? null : commentary.Trim();
    }

    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;

    public EmployeeDocumentCategory Category { get; private set; }
    public EmployeeDocumentType DocumentType { get; private set; }

    public string FileKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateOnly FileDate { get; private set; }
    public string? Commentary { get; private set; }

    public RqthDetail? RqthDetail { get; private set; }
    public ResidentPermitDetail? ResidentPermitDetail { get; private set; }

    public void UpdateMetadata(
        EmployeeDocumentType documentType,
        DateOnly fileDate,
        string? commentary)
    {
        DocumentType = documentType;
        Category = EmployeeDocumentTaxonomy.GetCategory(documentType);
        FileDate = fileDate;
        Commentary = string.IsNullOrWhiteSpace(commentary) ? null : commentary.Trim();
    }

    public void ReplaceFile(
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        DateOnly fileDate)
    {
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        FileDate = fileDate;
    }

    public void SetRqthDetail(DateOnly expiryDate, string? workplaceAccommodation)
    {
        ResidentPermitDetail = null;

        if (RqthDetail is not null)
        {
            RqthDetail.Update(expiryDate, workplaceAccommodation);
        }
        else
        {
            RqthDetail = new RqthDetail(Id, expiryDate, workplaceAccommodation);
        }
    }

    public void SetResidentPermitDetail(string permitNumber, DateOnly expiryDate)
    {
        RqthDetail = null;

        if (ResidentPermitDetail is not null)
        {
            ResidentPermitDetail.Update(permitNumber, expiryDate);
        }
        else
        {
            ResidentPermitDetail = new ResidentPermitDetail(Id, permitNumber, expiryDate);
        }
    }

    public void ClearSpecializedDetails()
    {
        RqthDetail = null;
        ResidentPermitDetail = null;
    }
}

public sealed class RqthDetail
{
    private RqthDetail()
    {
    }

    internal RqthDetail(Guid documentId, DateOnly expiryDate, string? workplaceAccommodation)
    {
        DocumentId = documentId;
        ExpiryDate = expiryDate;
        WorkplaceAccommodation = NormalizeAccommodation(workplaceAccommodation);
    }

    public Guid DocumentId { get; private set; }
    public EmployeeDocument Document { get; private set; } = null!;

    public DateOnly ExpiryDate { get; private set; }
    public string? WorkplaceAccommodation { get; private set; }
    public DateTime? ExpiryReminder30SentAtUtc { get; private set; }
    public DateTime? ExpiryReminder7SentAtUtc { get; private set; }

    internal void Update(DateOnly expiryDate, string? workplaceAccommodation)
    {
        if (ExpiryDate != expiryDate)
        {
            ExpiryReminder30SentAtUtc = null;
            ExpiryReminder7SentAtUtc = null;
        }

        ExpiryDate = expiryDate;
        WorkplaceAccommodation = NormalizeAccommodation(workplaceAccommodation);
    }

    private static string? NormalizeAccommodation(string? workplaceAccommodation) =>
        string.IsNullOrWhiteSpace(workplaceAccommodation) ? null : workplaceAccommodation.Trim();
}

public sealed class ResidentPermitDetail
{
    private ResidentPermitDetail()
    {
    }

    internal ResidentPermitDetail(Guid documentId, string permitNumber, DateOnly expiryDate)
    {
        DocumentId = documentId;
        PermitNumber = permitNumber.Trim();
        ExpiryDate = expiryDate;
    }

    public Guid DocumentId { get; private set; }
    public EmployeeDocument Document { get; private set; } = null!;

    public string PermitNumber { get; private set; } = string.Empty;
    public DateOnly ExpiryDate { get; private set; }
    public DateTime? ExpiryReminder30SentAtUtc { get; private set; }
    public DateTime? ExpiryReminder7SentAtUtc { get; private set; }

    internal void Update(string permitNumber, DateOnly expiryDate)
    {
        if (ExpiryDate != expiryDate)
        {
            ExpiryReminder30SentAtUtc = null;
            ExpiryReminder7SentAtUtc = null;
        }

        PermitNumber = permitNumber.Trim();
        ExpiryDate = expiryDate;
    }
}
