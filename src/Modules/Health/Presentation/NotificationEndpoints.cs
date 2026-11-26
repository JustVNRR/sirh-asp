using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Constants;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Presentation;

internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/notifications")
            .WithTags("Notifications");

        group.MapGet("/", async (
                bool? isRead,
                NotificationCategory? category,
                INotificationService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetMyNotificationsAsync(isRead, category, pagedRequest, cancellationToken);
                return MedicalVisitEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(HealthPermissions.NotificationsReadSelf)
            .Produces<PagedResult<NotificationDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/unread-count", async (
                INotificationService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetMyUnreadCountAsync(cancellationToken);
                return MedicalVisitEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(HealthPermissions.NotificationsReadSelf)
            .Produces<UnreadCountDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/{id:guid}/read", async (
                Guid id,
                INotificationService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.MarkAsReadAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : MedicalVisitEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HealthPermissions.NotificationsWriteSelf)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/read-all", async (
                INotificationService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.MarkAllAsReadAsync(cancellationToken);
                return result.IsSuccess ? Results.NoContent() : MedicalVisitEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HealthPermissions.NotificationsWriteSelf)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                INotificationService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : MedicalVisitEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HealthPermissions.NotificationsWriteSelf)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
