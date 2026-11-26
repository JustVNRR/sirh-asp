using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Infrastructure.Persistence;

public sealed class NotificationRepository(HealthDbContext dbContext) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<PagedResult<Notification>> GetPagedByRecipientAsync(
        Guid recipientEmployeeId,
        bool? isRead,
        NotificationCategory? category,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientEmployeeId == recipientEmployeeId);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(n => n.Category == category.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<Notification>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public Task<int> GetUnreadCountAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientEmployeeId == recipientEmployeeId && !n.IsRead, cancellationToken);

    public async Task MarkAllAsReadAsync(
        Guid recipientEmployeeId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default)
    {
        var unread = await dbContext.Notifications
            .Where(n => n.RecipientEmployeeId == recipientEmployeeId && !n.IsRead)
            .ToArrayAsync(cancellationToken);

        foreach (var item in unread)
        {
            item.MarkAsRead(readAtUtc);
        }
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.AddAsync(notification, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.AddRangeAsync(notifications, cancellationToken);

    public void Remove(Notification notification) => dbContext.Notifications.Remove(notification);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
