using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Application.Abstractions;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Notification>> GetPagedByRecipientAsync(
        Guid recipientEmployeeId,
        bool? isRead,
        NotificationCategory? category,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid recipientEmployeeId, DateTime readAtUtc, CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);

    void Remove(Notification notification);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
