using Sirh.Modules.Health.Application.DTOs;

namespace Sirh.Modules.Health.Application.Abstractions;

public interface IDailyHealthRemindersJob
{
    Task<ReminderRunSummary> RunAsync(CancellationToken cancellationToken = default);
}
