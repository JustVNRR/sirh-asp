using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Contracts;

namespace Sirh.Modules.Health.Infrastructure.Persistence;

public sealed class NotificationLookupService(HealthDbContext dbContext) : INotificationLookup
{
    public Task<int> GetUnreadCountAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientEmployeeId == recipientEmployeeId && !n.IsRead, cancellationToken);
}
