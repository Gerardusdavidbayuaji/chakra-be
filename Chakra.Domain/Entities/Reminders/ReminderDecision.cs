using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities.Reminders;

// Hasil Premi.EvaluateReminders. Kind null = tidak ada pesan ke nasabah hari ini
public sealed record ReminderDecision(
    Installment? Target,
    ReminderKind? Kind,
    int DaysLate,
    DateOnly? GracePeriodEndsOn,
    bool AdminAlertDue)
{
    public static ReminderDecision None { get; } = new(null, null, 0, null, false);
}
