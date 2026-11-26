namespace Sirh.BuildingBlocks.Application.Contracts;

public interface INotificationLookup
{
    Task<int> GetUnreadCountAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default);
}
