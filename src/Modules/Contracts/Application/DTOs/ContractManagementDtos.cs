using Sirh.Modules.Contracts.Domain.Enums;

namespace Sirh.Modules.Contracts.Application.DTOs;

public sealed record DownloadedFileDto(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes);

public sealed record UploadedPdfInput(
    Stream Content,
    string FileName,
    string ContentType);

public sealed record ImportContractCommand(
    Guid PositionId,
    Guid ContractTypeId,
    ContractStatus Status,
    DateOnly? SignedAt,
    string? Commentary,
    UploadedPdfInput File);

public sealed record UpdateContractCommand(
    Guid ContractTypeId,
    ContractStatus Status,
    DateOnly? SignedAt,
    string? Commentary,
    UploadedPdfInput? File,
    string? RowVersion);

public sealed record JobSheetDto(
    Guid Id,
    Guid PositionId,
    Guid EmployeeId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateOnly FileDate,
    string? Commentary,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record CreateJobSheetCommand(
    Guid PositionId,
    string? Commentary,
    UploadedPdfInput File);

public sealed record UpdateJobSheetCommand(
    string? Commentary,
    UploadedPdfInput? File,
    string? RowVersion);
