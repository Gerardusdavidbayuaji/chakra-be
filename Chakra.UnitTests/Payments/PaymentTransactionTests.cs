using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Events;
using Chakra.Domain.Exceptions;
using Chakra.UnitTests.Support;

namespace Chakra.UnitTests.PaymentsDomain;

public class PaymentTransactionTests
{
    private static readonly DateTime Now = PremiFactory.Now;

    private static PaymentTransaction NewTransaction()
    {
        var premi = PremiFactory.Create();
        return PaymentTransaction.Create(PremiFactory.Nth(premi, 1), Now, 24);
    }

    [Fact]
    public void Create_FromInstallment_SetsPendingState()
    {
        var premi = PremiFactory.Create();
        var installment = PremiFactory.Nth(premi, 1);

        var tx = PaymentTransaction.Create(installment, Now, 24);

        Assert.Equal(PaymentTransactionStatus.Pending, tx.Status);
        Assert.StartsWith("CHK-", tx.OrderId);
        Assert.Equal(36, tx.OrderId.Length);
        Assert.Equal(installment.Amount, tx.Amount);
        Assert.Equal(installment.Id, tx.InstallmentId);
        Assert.Equal(premi.Id, tx.PremiId);
        Assert.Equal(Now.AddHours(24), tx.ExpiresAt);
    }

    [Fact]
    public void MarkPaid_FromExpired_IsAllowed()
    {
        var tx = NewTransaction();
        tx.MarkExpired();

        tx.MarkPaid(Now, "qris", "mid-1");

        Assert.Equal(PaymentTransactionStatus.Paid, tx.Status);
        Assert.Equal(Now, tx.SettledAt);
    }

    [Fact]
    public void MarkUnapplied_RaisesPaymentUnappliedEvent()
    {
        var tx = NewTransaction();

        tx.MarkUnapplied(PaymentRejectionReason.AmountMismatch, Now, "qris", "mid-1");

        var evt = Assert.Single(tx.DomainEvents.OfType<PaymentUnappliedEvent>());
        Assert.Equal(PaymentTransactionStatus.Unapplied, tx.Status);
        Assert.Equal(PaymentRejectionReason.AmountMismatch, evt.Reason);
        Assert.Equal(tx.OrderId, evt.OrderId);
    }

    [Fact]
    public void MarkAllocated_FromPending_Throws()
    {
        var tx = NewTransaction();

        var ex = Assert.Throws<DomainException>(() =>
            tx.MarkAllocated(InstallmentId.New(), UserId.New(), Now, null));

        Assert.Equal("Payment cannot change from Pending.", ex.Message);
    }

    [Fact]
    public void IsFinal_PerStatus()
    {
        var pending = NewTransaction();
        Assert.False(pending.IsFinal);

        var failed = NewTransaction();
        failed.MarkFailed();
        Assert.False(failed.IsFinal);

        var expired = NewTransaction();
        expired.MarkExpired();
        Assert.False(expired.IsFinal);

        var paid = NewTransaction();
        paid.MarkPaid(Now, null, null);
        Assert.True(paid.IsFinal);

        var unapplied = NewTransaction();
        unapplied.MarkUnapplied(PaymentRejectionReason.PremiNotActive, Now, null, null);
        Assert.True(unapplied.IsFinal);

        var allocated = NewTransaction();
        allocated.MarkUnapplied(PaymentRejectionReason.PremiNotActive, Now, null, null);
        allocated.MarkAllocated(InstallmentId.New(), UserId.New(), Now, "ok");
        Assert.True(allocated.IsFinal);
        Assert.Equal(PaymentTransactionStatus.Allocated, allocated.Status);

        var refunded = NewTransaction();
        refunded.MarkUnapplied(PaymentRejectionReason.PremiNotActive, Now, null, null);
        refunded.MarkRefunded("ref-1", null, Now, null);
        Assert.True(refunded.IsFinal);
        Assert.Equal(PaymentTransactionStatus.Refunded, refunded.Status);
    }
}
