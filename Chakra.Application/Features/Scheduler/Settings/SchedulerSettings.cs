using Chakra.Domain.Entities.Reminders;

namespace Chakra.Application.Features.Scheduler.Settings;

// Days: sekali sehari setelah RunAtLocalTime. Minutes: 1 menit nyata = 1 hari simulasi (hanya Development)
public enum SchedulerTimeScale
{
    Days,
    Minutes
}

public class SchedulerSettings
{
    public const string SectionName = "Scheduler";
    public const string DailyRemindersJobName = "DailyReminders";

    public string RunAtLocalTime { get; set; } = "09:00";
    public string TimeZone { get; set; } = "Asia/Jakarta";
    public int CheckIntervalMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 3;
    public int BatchSize { get; set; } = 100;
    public int DaysBeforeFirst { get; set; } = 3;
    public int DaysBeforeSecond { get; set; } = 1;
    public int RepeatEveryDays { get; set; } = 3;
    public int AdminAlertAfterDueDays { get; set; } = 27;
    public SchedulerTimeScale TimeScale { get; set; } = SchedulerTimeScale.Days;
    public DateOnly? SimulationStartDate { get; set; }

    public ReminderPolicy ToPolicy() =>
        new(DaysBeforeFirst, DaysBeforeSecond, RepeatEveryDays, AdminAlertAfterDueDays);
}
