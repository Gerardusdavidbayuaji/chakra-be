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

    internal void Cancel()
    {
        Status = InstallmentStatus.Cancelled;
    }
}
