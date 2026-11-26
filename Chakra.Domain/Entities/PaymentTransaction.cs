using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Events;
using Chakra.Domain.Exceptions;

namespace Chakra.Domain.Entities;

// Satu percobaan pembayaran Midtrans untuk satu cicilan. Semua transisi status ada di sini.
public class PaymentTransaction : Aggregate<PaymentTransactionId>
{
    public string OrderId { get; private set; } = null!; // order_id Midtrans, unik
    public InstallmentId InstallmentId { get; private set; } // cicilan yang dituju saat link dibuat
    public PremiId PremiId { get; private set; } // denormalisasi untuk query
    public decimal Amount { get; private set; } // = Installment.Amount saat dibuat (gross_amount)
    public PaymentTransactionStatus Status { get; private set; }
    public string? SnapToken { get; private set; }
    public string? RedirectUrl { get; private set; }
    public DateTime ExpiresAt { get; private set; } // UTC
    public bool ExpireRequested { get; private set; }
    public string? PaymentType { get; private set; }
    public string? MidtransTransactionId { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public PaymentRejectionReason? RejectionReason { get; private set; }
    public InstallmentId? AllocatedInstallmentId { get; private set; }
    public string? RefundReference { get; private set; }
    public string? ResolutionNote { get; private set; }
    public UserId? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public uint RowVersion { get; private set; }

    public bool IsFinal => Status is PaymentTransactionStatus.Paid
        or PaymentTransactionStatus.Unapplied
        or PaymentTransactionStatus.Allocated
        or PaymentTransactionStatus.Refunded;

    private PaymentTransaction()
    {
    }

    public static PaymentTransaction Create(Installment installment, DateTime now, int expiryHours) =>
        new()
        {
            Id = PaymentTransactionId.New(),
            OrderId = "CHK-" + Guid.CreateVersion7().ToString("N"),
            InstallmentId = installment.Id,
            PremiId = installment.PremiId,
            Amount = installment.Amount,
            Status = PaymentTransactionStatus.Pending,
            ExpiresAt = now.AddHours(expiryHours)
        };

    public void AttachSnap(string token, string redirectUrl)
    {
        EnsureStatus(PaymentTransactionStatus.Pending);
        SnapToken = token;
        RedirectUrl = redirectUrl;
    }

    public void RequestExpire()
    {
        EnsureStatus(PaymentTransactionStatus.Pending);
        ExpireRequested = true;
    }

    public void MarkPaid(DateTime settledAt, string? paymentType, string? midtransTransactionId)
    {
        EnsureStatus(PaymentTransactionStatus.Pending, PaymentTransactionStatus.Expired,
            PaymentTransactionStatus.Failed);
        Status = PaymentTransactionStatus.Paid;
        Settle(settledAt, paymentType, midtransTransactionId);
    }

    public void MarkUnapplied(PaymentRejectionReason reason, DateTime settledAt, string? paymentType,
        string? midtransTransactionId)
    {
        EnsureStatus(PaymentTransactionStatus.Pending, PaymentTransactionStatus.Expired,
            PaymentTransactionStatus.Failed);
        Status = PaymentTransactionStatus.Unapplied;
        RejectionReason = reason;
        Settle(settledAt, paymentType, midtransTransactionId);

        AddDomainEvent(new PaymentUnappliedEvent(Id, PremiId, OrderId, Amount, reason));
    }

    public void MarkFailed()
    {
        EnsureStatus(PaymentTransactionStatus.Pending);
        Status = PaymentTransactionStatus.Failed;
    }

    public void MarkExpired()
    {
        EnsureStatus(PaymentTransactionStatus.Pending);
        Status = PaymentTransactionStatus.Expired;
    }

    public void MarkAllocated(InstallmentId target, UserId adminId, DateTime now, string? note)
    {
        EnsureStatus(PaymentTransactionStatus.Unapplied);
        Status = PaymentTransactionStatus.Allocated;
        AllocatedInstallmentId = target;
        Resolve(adminId, now, note);
    }

    public void MarkRefunded(string reference, UserId? adminId, DateTime now, string? note)
    {
        EnsureStatus(PaymentTransactionStatus.Unapplied);
        Status = PaymentTransactionStatus.Refunded;
        RefundReference = reference;
        Resolve(adminId, now, note);
    }

    private void Settle(DateTime settledAt, string? paymentType, string? midtransTransactionId)
    {
        SettledAt = settledAt;
        PaymentType = paymentType;
        MidtransTransactionId = midtransTransactionId;
    }

    private void Resolve(UserId? adminId, DateTime now, string? note)
    {
        ResolvedByUserId = adminId;
        ResolvedAt = now;
        ResolutionNote = note;
    }

    private void EnsureStatus(params PaymentTransactionStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException($"Payment cannot change from {Status}.");
    }
}
