using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;

namespace Sirh.Modules.Identity.Presentation;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var rolesGroup = endpoints.MapGroup("/api/v1/roles")
            .WithTags("Roles & Permissions");

        rolesGroup.MapGet("/", async (
                IRoleManagementService roleManagementService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await roleManagementService.GetRolesAsync(cancellationToken);
                return AuthEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(IdentityPermissions.RolesRead)
            .Produces<IReadOnlyList<RoleSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        rolesGroup.MapGet("/{id:guid}/permissions", async (
                Guid id,
                IRoleManagementService roleManagementService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await roleManagementService.GetRolePermissionsAsync(id, cancellationToken);
                return AuthEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(IdentityPermissions.RolesRead)
            .Produces<RolePermissionsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        rolesGroup.MapPut("/{id:guid}/permissions", async (
                Guid id,
                UpdateRolePermissionsRequest request,
                ICurrentUser currentUser,
                IRoleManagementService roleManagementService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await roleManagementService.UpdateRolePermissionsAsync(
                    id,
                    request,
                    currentUser.Roles,
                    cancellationToken);
                return AuthEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(IdentityPermissions.RolesManagePermissions)
            .Produces<RolePermissionsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/v1/permissions", async (
                IRoleManagementService roleManagementService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await roleManagementService.GetAllPermissionsAsync(cancellationToken);
                return AuthEndpoints.ToHttpResult(result, httpContext);
            })
            .WithTags("Roles & Permissions")
            .RequireAuthorization(IdentityPermissions.RolesRead)
            .Produces<IReadOnlyList<PermissionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
