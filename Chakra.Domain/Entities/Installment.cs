using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

public class Installment
{
    public Guid Id { get; set; }
    public Guid PremiId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public InstallmentStatus Status { get; set; }
    public int ReminderCount { get; set; }
    public string? MidtransOrderId { get; set; }
    public DateTime? PaidAt { get; set; }
    public uint RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }

    public Premi Premi { get; set; } = null!;
}