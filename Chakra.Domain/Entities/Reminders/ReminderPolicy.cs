namespace Chakra.Domain.Entities.Reminders;

// Nilai dari settings Scheduler: H-3, H-1, ulang tiap 3 hari sejak H, alert admin di D+27
public sealed record ReminderPolicy(
    int DaysBeforeFirst,
    int DaysBeforeSecond,
    int RepeatEveryDays,
    int AdminAlertAfterDueDays);
