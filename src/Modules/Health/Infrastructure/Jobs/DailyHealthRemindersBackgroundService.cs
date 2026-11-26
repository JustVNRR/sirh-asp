using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.Modules.Health.Application.Abstractions;

namespace Sirh.Modules.Health.Infrastructure.Jobs;

public sealed class DailyHealthRemindersBackgroundService(
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider dateTimeProvider,
    IOptions<HealthRemindersOptions> options,
    ILogger<DailyHealthRemindersBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Daily health reminders background service is disabled.");
            return;
        }

        var timeZone = ResolveTimeZone(options.Value.TimeZoneId);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelayUntilNextRun(dateTimeProvider.UtcNow, options.Value, timeZone);
            logger.LogDebug("Next daily health reminders run scheduled in {Delay}.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var job = scope.ServiceProvider.GetRequiredService<IDailyHealthRemindersJob>();
                await job.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while executing daily health reminders job.");
            }
        }
    }

    internal static TimeSpan ComputeDelayUntilNextRun(
        DateTime utcNow,
        HealthRemindersOptions opts,
        TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        var scheduledLocal = new DateTime(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            Math.Clamp(opts.RunAtHourLocal, 0, 23),
            Math.Clamp(opts.RunAtMinuteLocal, 0, 59),
            0,
            DateTimeKind.Unspecified);

        if (scheduledLocal <= localNow)
        {
            scheduledLocal = scheduledLocal.AddDays(1);
        }

        var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(scheduledLocal, timeZone);
        var delay = nextRunUtc - utcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
