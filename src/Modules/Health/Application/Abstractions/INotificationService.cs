using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Application.Abstractions;

public interface INotificationService
{
    Task<Result<PagedResult<NotificationDto>>> GetMyNotificationsAsync(
        bool? isRead,
        NotificationCategory? category,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<UnreadCountDto>> GetMyUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<Result> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> MarkAllAsReadAsync(CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
