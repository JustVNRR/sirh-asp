namespace Sirh.Modules.Health.Infrastructure.Jobs;

public sealed class HealthRemindersOptions
{
    public const string SectionName = "Health:Reminders";

    public bool Enabled { get; set; } = true;
    public int RunAtHourLocal { get; set; } = 4;
    public int RunAtMinuteLocal { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Paris";
}
