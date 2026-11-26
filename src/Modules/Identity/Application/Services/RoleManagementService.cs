using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Application.Services;

public sealed class RoleManagementService : IRoleManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IValidator<UpdateRolePermissionsRequest> _updateValidator;
    private readonly ILogger<RoleManagementService> _logger;
    private readonly INotificationLookup? _notificationLookup;

    public RoleManagementService(
        UserManager<ApplicationUser> userManager,
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository,
        IValidator<UpdateRolePermissionsRequest> updateValidator,
        ILogger<RoleManagementService> logger,
        INotificationLookup? notificationLookup = null)
    {
        _userManager = userManager;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _updateValidator = updateValidator;
        _logger = logger;
        _notificationLookup = notificationLookup;
    }

    public async Task<Result<CurrentUserProfileResponse>> GetCurrentUserProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return Error.Unauthorized("Auth.UserNotFound", "Authenticated user was not found or is inactive.");
        }

        var roleNames = (await _userManager.GetRolesAsync(user)).ToArray();
        var roleEntities = await _rolePermissionRepository.GetRolesByNamesOrCodesAsync(roleNames, cancellationToken);
        var permissions = await _permissionRepository.GetPermissionsForRolesAsync(roleNames, cancellationToken);

        var roles = roleEntities
            .OrderByDescending(r => r.Priority)
            .Select(r => new UserRoleSummaryDto(r.Id, r.Code, r.Name ?? r.Code, r.Priority))
            .ToArray();

        var maxPriority = roles.Length > 0 ? roles.Max(r => r.Priority) : 0;
        var unreadCount = user.EmployeeId.HasValue && _notificationLookup is not null
            ? await _notificationLookup.GetUnreadCountAsync(user.EmployeeId.Value, cancellationToken)
            : 0;

        return new CurrentUserProfileResponse(
            user.Id,
            user.EmployeeId,
            user.Email ?? string.Empty,
            user.IsActive,
            maxPriority,
            roles,
            permissions.ToArray(),
            unreadCount);
    }

    public async Task<Result<IReadOnlyList<RoleSummaryDto>>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        var roles = await _rolePermissionRepository.GetAllRolesWithPermissionsAsync(cancellationToken);

        IReadOnlyList<RoleSummaryDto> response = roles
            .OrderBy(r => r.Priority)
            .Select(r => new RoleSummaryDto(
                r.Id,
                r.Code,
                r.Name ?? r.Code,
                r.Priority,
                r.Description,
                r.RolePermissions.Count))
            .ToArray();

        return Result.Success(response);
    }

    public async Task<Result<IReadOnlyList<PermissionDto>>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var permissions = await _rolePermissionRepository.GetAllPermissionsAsync(cancellationToken);

        IReadOnlyList<PermissionDto> response = permissions
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Code)
            .Select(ToPermissionDto)
            .ToArray();

        return Result.Success(response);
    }

    public async Task<Result<RolePermissionsResponse>> GetRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _rolePermissionRepository.GetRoleWithPermissionsAsync(roleId, cancellationToken);
        if (role is null)
        {
            return Error.NotFound("Roles.NotFound", $"Role '{roleId}' was not found.");
        }

        var permissions = role.RolePermissions
            .Select(rp => rp.Permission)
            .OrderBy(p => p.Code)
            .Select(ToPermissionDto)
            .ToArray();

        return new RolePermissionsResponse(
            role.Id,
            role.Code,
            role.Name ?? role.Code,
            role.Priority,
            permissions);
    }

    public async Task<Result<RolePermissionsResponse>> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        IReadOnlyCollection<string> callerRoles,
        CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var role = await _rolePermissionRepository.GetRoleWithPermissionsAsync(roleId, cancellationToken);
        if (role is null)
        {
            return Error.NotFound("Roles.NotFound", $"Role '{roleId}' was not found.");
        }

        if (string.Equals(role.Code, IdentityRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            var callerRoleEntities = await _rolePermissionRepository.GetRolesByNamesOrCodesAsync(callerRoles, cancellationToken);
            var isCallerSuperAdmin = callerRoleEntities.Any(r =>
                    string.Equals(r.Code, IdentityRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                || callerRoles.Any(r =>
                    string.Equals(r, IdentityRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));

            if (!isCallerSuperAdmin)
            {
                return Error.Forbidden(
                    "Roles.SuperAdminProtected",
                    "Only a SUPER_ADMIN user can modify permissions assigned to the SUPER_ADMIN role.");
            }
        }

        var normalizedCodes = request.PermissionCodes
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = await _rolePermissionRepository.GetPermissionsByCodesAsync(normalizedCodes, cancellationToken);
        var unknownCodes = normalizedCodes
            .Except(permissions.Select(p => p.Code), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unknownCodes.Length > 0)
        {
            return Error.Validation(
                "Roles.UnknownPermissions",
                $"Unknown permission code(s): {string.Join(", ", unknownCodes)}.");
        }

        await _rolePermissionRepository.ReplaceRolePermissionsAsync(
            role.Id,
            permissions.Select(p => p.Id).ToArray(),
            cancellationToken);
        await _rolePermissionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated permissions for role {RoleId} ({RoleCode}): {PermissionCount} permission(s) assigned.",
            role.Id,
            role.Code,
            permissions.Count);

        return new RolePermissionsResponse(
            role.Id,
            role.Code,
            role.Name ?? role.Code,
            role.Priority,
            permissions.OrderBy(p => p.Code).Select(ToPermissionDto).ToArray());
    }

    private static PermissionDto ToPermissionDto(Permission permission) =>
        new(permission.Id, permission.Code, permission.Module, permission.Description);
}
