namespace Sirh.Modules.Identity.Application.DTOs;

public sealed record UserRoleSummaryDto(Guid Id, string Code, string Name, int Priority);

public sealed record CurrentUserProfileResponse(
    Guid UserId,
    Guid? EmployeeId,
    string Email,
    bool IsActive,
    int MaxRolePriority,
    IReadOnlyList<UserRoleSummaryDto> Roles,
    IReadOnlyList<string> Permissions,
    int UnreadNotificationsCount);

public sealed record PermissionDto(Guid Id, string Code, string Module, string Description);

public sealed record RoleSummaryDto(
    Guid Id,
    string Code,
    string Name,
    int Priority,
    string Description,
    int PermissionCount);

public sealed record RolePermissionsResponse(
    Guid RoleId,
    string Code,
    string Name,
    int Priority,
    IReadOnlyList<PermissionDto> Permissions);

public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> PermissionCodes);
