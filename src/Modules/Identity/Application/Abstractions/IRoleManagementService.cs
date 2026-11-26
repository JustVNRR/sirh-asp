using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.DTOs;

namespace Sirh.Modules.Identity.Application.Abstractions;

public interface IRoleManagementService
{
    Task<Result<CurrentUserProfileResponse>> GetCurrentUserProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RoleSummaryDto>>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PermissionDto>>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<RolePermissionsResponse>> GetRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task<Result<RolePermissionsResponse>> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        IReadOnlyCollection<string> callerRoles,
        CancellationToken cancellationToken = default);
}
