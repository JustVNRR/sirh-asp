using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.DTOs;

public sealed record CommonDocumentDto(
    Guid Id,
    Guid? CompanyId,
    Guid? ContractTypeId,
    CommonDocumentCategory Category,
    bool IsWelcomeKit,
    string Title,
    string? Description,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateOnly FileDate,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record DownloadedCommonFileDto(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes);

public sealed record UploadedCommonFileInput(
    Stream Content,
    string FileName,
    string ContentType);

public sealed record CommonDocumentQuery(
    Guid? CompanyId,
    CommonDocumentCategory? Category,
    bool? IsWelcomeKit,
    Guid? ContractTypeId,
    bool? Archived,
    string? Search);

public sealed record CreateCommonDocumentCommand(
    Guid? CompanyId,
    Guid? ContractTypeId,
    CommonDocumentCategory Category,
    bool? IsWelcomeKit,
    string Title,
    string? Description,
    DateOnly? FileDate,
    UploadedCommonFileInput File);

public sealed record UpdateCommonDocumentCommand(
    Guid? CompanyId,
    Guid? ContractTypeId,
    CommonDocumentCategory Category,
    bool? IsWelcomeKit,
    string Title,
    string? Description,
    DateOnly? FileDate,
    UploadedCommonFileInput? File,
    string? RowVersion);

public sealed record ArchiveCommonDocumentRequest(
    bool IsArchived,
    string? RowVersion = null);
