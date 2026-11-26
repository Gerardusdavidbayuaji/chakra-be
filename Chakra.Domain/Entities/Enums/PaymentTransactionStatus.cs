namespace Chakra.Domain.Entities.Enums;

public enum PaymentTransactionStatus
{
    Pending,
    Paid,
    Failed,
    Expired,
    Unapplied,
    Allocated,
    Refunded
}
