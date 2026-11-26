using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.Contracts.Domain.Entities;

public sealed class JobSheet : AuditableEntity
{
    private JobSheet()
    {
    }

    public JobSheet(
        Guid positionId,
        Guid employeeId,
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        DateOnly fileDate,
        string? commentary)
    {
        PositionId = positionId;
        EmployeeId = employeeId;
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        FileDate = fileDate;
        Commentary = string.IsNullOrWhiteSpace(commentary) ? null : commentary.Trim();
    }

    public Guid PositionId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string FileKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateOnly FileDate { get; private set; }
    public string? Commentary { get; private set; }

    public void UpdateMetadata(string? commentary)
    {
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
}
