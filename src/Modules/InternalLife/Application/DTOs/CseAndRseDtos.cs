using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.DTOs;

public sealed record StaffRepresentativeDto(
    Guid Id,
    Guid CompanyId,
    Guid? EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    StaffRepresentativeRole RepresentativeRole,
    ElectoralCollege College,
    bool IsHarassmentAdvisor,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record CreateStaffRepresentativeRequest(
    Guid CompanyId,
    Guid? EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    StaffRepresentativeRole RepresentativeRole,
    ElectoralCollege College,
    bool IsHarassmentAdvisor);

public sealed record UpdateStaffRepresentativeRequest(
    Guid CompanyId,
    Guid? EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    StaffRepresentativeRole RepresentativeRole,
    ElectoralCollege College,
    bool IsHarassmentAdvisor,
    string? RowVersion = null);

public sealed record RseIdeaDto(
    Guid Id,
    Guid AuthorEmployeeId,
    Guid? LocationId,
    string Title,
    string IdeaContent,
    bool HasAttachment,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentFileSizeBytes,
    RseIdeaStatus Status,
    string? RhComment,
    Guid? TreatedByEmployeeId,
    DateTime? TreatedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record RseIdeaQuery(
    Guid? LocationId,
    RseIdeaStatus? Status,
    Guid? AuthorId,
    bool? MineOnly,
    string? Search);

public sealed record CreateRseIdeaCommand(
    Guid? LocationId,
    string Title,
    string IdeaContent,
    UploadedCommonFileInput? Attachment);

public sealed record UpdateRseIdeaCommand(
    Guid? LocationId,
    string Title,
    string IdeaContent,
    UploadedCommonFileInput? Attachment,
    string? RowVersion);

public sealed record TreatRseIdeaRequest(
    RseIdeaStatus Status,
    string? RhComment,
    string? RowVersion = null);
