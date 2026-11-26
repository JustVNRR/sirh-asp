using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.Contracts.Domain.Enums;

namespace Sirh.Modules.Contracts.Domain.Entities;

public sealed class Contract : AuditableEntity
{
    private Contract()
    {
    }

    public Contract(
        Guid positionId,
        Guid employeeId,
        Guid contractTypeId,
        Guid? contractModelId,
        ContractStatus status,
        DateOnly? signedAt,
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        DateOnly fileDate,
        string? commentary)
    {
        PositionId = positionId;
        EmployeeId = employeeId;
        ContractTypeId = contractTypeId;
        ContractModelId = contractModelId;
        Status = status;
        SignedAt = signedAt;
        FileKey = fileKey;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        FileDate = fileDate;
        Commentary = string.IsNullOrWhiteSpace(commentary) ? null : commentary.Trim();
    }

    public Guid PositionId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ContractTypeId { get; private set; }
    public Guid? ContractModelId { get; private set; }
    public ContractModel? ContractModel { get; private set; }

    public ContractStatus Status { get; private set; }
    public DateOnly? SignedAt { get; private set; }
    public string FileKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateOnly FileDate { get; private set; }
    public string? Commentary { get; private set; }

    public void UpdateMetadata(
        Guid contractTypeId,
        ContractStatus status,
        DateOnly? signedAt,
        string? commentary)
    {
        ContractTypeId = contractTypeId;
        Status = status;
        SignedAt = signedAt;
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
