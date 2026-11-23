using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

// Hanya dibuat dan diubah lewat aggregate Premi
public class Installment : Aggregate<InstallmentId>
{
    public PremiId PremiId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public InstallmentStatus Status { get; private set; }
    public int ReminderCount { get; private set; }
    public string? MidtransOrderId { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateOnly? LastRemindedOn { get; private set; } // tanggal (zona bisnis) pengingat terakhir diantrekan
    public DateOnly? OverdueAlertSentOn { get; private set; } // tanggal alert admin dikirim
    public uint RowVersion { get; private set; }

    public Premi Premi { get; private set; } = null!;

    private Installment()
    {
    }

    internal static Installment Create(PremiId premiId, int installmentNumber, DateOnly dueDate, decimal amount) =>
        new()
        {
            Id = InstallmentId.New(),
            PremiId = premiId,
            InstallmentNumber = installmentNumber,
            DueDate = dueDate,
            Amount = amount,
            Status = InstallmentStatus.Pending,
            ReminderCount = 0
        };

    internal void MarkPaid(DateTime paidAt, string? midtransOrderId)
    {
        Status = InstallmentStatus.Paid;
        PaidAt = paidAt;
        if (midtransOrderId != null)
            MidtransOrderId = midtransOrderId;
    }

    // Hanya maju: Pending -> Reminder1 -> Reminder2 -> Overdue. Paid/Cancelled tidak disentuh
    internal void PromoteTo(InstallmentStatus status)
    {
        var current = Rank(Status);
        var target = Rank(status);
        if (current < 0 || target < 0 || target <= current)
            return;

        Status = status;
    }

    internal void RecordReminder(DateOnly today)
    {
        LastRemindedOn = today;
        ReminderCount++;
    }

    internal void RecordOverdueAlert(DateOnly today)
    {
        OverdueAlertSentOn = today;
    }

    private static int Rank(InstallmentStatus status) => status switch
    {
        InstallmentStatus.Pending => 0,
        InstallmentStatus.Reminder1 => 1,
        InstallmentStatus.Reminder2 => 2,
        InstallmentStatus.Overdue => 3,
        _ => -1
    };

    internal void Cancel()
    {
        Status = InstallmentStatus.Cancelled;
    }
}
