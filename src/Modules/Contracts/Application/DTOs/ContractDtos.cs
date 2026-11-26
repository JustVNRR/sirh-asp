using Sirh.Modules.Contracts.Domain.Enums;

namespace Sirh.Modules.Contracts.Application.DTOs;

public sealed record ContractModelDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? ContractTypeId,
    string TemplateContent,
    IReadOnlyList<string> CustomVariables,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record CreateContractModelRequest(
    string Title,
    string? Description,
    Guid? ContractTypeId,
    string TemplateContent,
    IReadOnlyList<string>? CustomVariables = null,
    bool IsActive = true);

public sealed record UpdateContractModelRequest(
    string Title,
    string? Description,
    Guid? ContractTypeId,
    string TemplateContent,
    IReadOnlyList<string>? CustomVariables = null,
    bool IsActive = true,
    string? RowVersion = null);

public sealed record PreviewContractRequest(
    Guid PositionId,
    Dictionary<string, string>? CustomVariables = null);

public sealed record PreviewPdfResult(byte[] Content, string FileName);

public sealed record GenerateContractRequest(
    Guid ContractModelId,
    Guid PositionId,
    Guid ContractTypeId,
    Dictionary<string, string>? CustomVariables = null,
    string? Commentary = null);

public sealed record ContractDto(
    Guid Id,
    Guid PositionId,
    Guid EmployeeId,
    Guid ContractTypeId,
    Guid? ContractModelId,
    ContractStatus Status,
    DateOnly? SignedAt,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateOnly FileDate,
    string? Commentary,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);
