using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Domain.Entities;

public sealed class CommonDocument : AuditableEntity
{
    private CommonDocument()
    {
    }

    public CommonDocument(
        Guid? companyId,
        Guid? contractTypeId,
        CommonDocumentCategory category,
        bool isWelcomeKit,
        string title,
        string? description,
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        DateOnly fileDate)
    {
        CompanyId = companyId;
        ContractTypeId = contractTypeId;
        Category = category;
        IsWelcomeKit = isWelcomeKit;
        Title = title.Trim();
        Description = NormalizeOptionalText(description);
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        FileDate = fileDate;
        IsArchived = false;
    }

    public Guid? CompanyId { get; private set; }
    public Guid? ContractTypeId { get; private set; }
    public CommonDocumentCategory Category { get; private set; }
    public bool IsWelcomeKit { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public string FileKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateOnly FileDate { get; private set; }
    public bool IsArchived { get; private set; }

    public void UpdateMetadata(
        Guid? companyId,
        Guid? contractTypeId,
        CommonDocumentCategory category,
        bool isWelcomeKit,
        string title,
        string? description,
        DateOnly fileDate)
    {
        CompanyId = companyId;
        ContractTypeId = contractTypeId;
        Category = category;
        IsWelcomeKit = isWelcomeKit;
        Title = title.Trim();
        Description = NormalizeOptionalText(description);
        FileDate = fileDate;
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

    public void SetArchived(bool isArchived)
    {
        IsArchived = isArchived;
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
