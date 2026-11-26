using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Application.Services;

public sealed class NotificationService(
    INotificationRepository notificationRepository,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser) : INotificationService
{
    public async Task<Result<PagedResult<NotificationDto>>> GetMyNotificationsAsync(
        bool? isRead,
        NotificationCategory? category,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error))
        {
            return error!;
        }

        var page = await notificationRepository.GetPagedByRecipientAsync(
            employeeId,
            isRead,
            category,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(MapToDto).ToArray();
        return PagedResult<NotificationDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<UnreadCountDto>> GetMyUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error))
        {
            return error!;
        }

        var count = await notificationRepository.GetUnreadCountAsync(employeeId, cancellationToken);
        return new UnreadCountDto(count);
    }

    public async Task<Result> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error))
        {
            return Result.Failure(error!);
        }

        var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return Result.Failure(Error.NotFound("Notifications.NotFound", $"Notification '{id}' was not found."));
        }

        if (notification.RecipientEmployeeId != employeeId)
        {
            return Result.Failure(Error.Forbidden(
                "Notifications.Forbidden",
                "You can only mark your own notifications as read."));
        }

        notification.MarkAsRead(dateTimeProvider.UtcNow);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error))
        {
            return Result.Failure(error!);
        }

        await notificationRepository.MarkAllAsReadAsync(employeeId, dateTimeProvider.UtcNow, cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var error))
        {
            return Result.Failure(error!);
        }

        var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return Result.Failure(Error.NotFound("Notifications.NotFound", $"Notification '{id}' was not found."));
        }

        if (notification.RecipientEmployeeId != employeeId)
        {
            return Result.Failure(Error.Forbidden(
                "Notifications.Forbidden",
                "You can only delete your own notifications."));
        }

        notificationRepository.Remove(notification);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private bool TryGetEmployeeId(out Guid employeeId, out Error? error)
    {
        if (!currentUser.IsAuthenticated || !currentUser.EmployeeId.HasValue || currentUser.EmployeeId.Value == Guid.Empty)
        {
            employeeId = Guid.Empty;
            error = Error.Forbidden(
                "Notifications.Forbidden",
                "Your account must be linked to an employee profile to access notifications.");
            return false;
        }

        employeeId = currentUser.EmployeeId.Value;
        error = null;
        return true;
    }

    private static NotificationDto MapToDto(Notification notification) =>
        new(
            notification.Id,
            notification.RecipientEmployeeId,
            notification.Category,
            notification.ReferenceEntity,
            notification.ReferenceId,
            notification.Subject,
            notification.Content,
            notification.IsRead,
            notification.ReadAtUtc,
            notification.EmailSent,
            notification.CreatedAtUtc);
}
