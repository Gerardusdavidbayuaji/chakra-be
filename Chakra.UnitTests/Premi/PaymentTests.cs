using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Events;
using Chakra.Domain.Exceptions;
using Chakra.UnitTests.Support;

namespace Chakra.UnitTests.PremiPaymentTests;

public class PaymentTests
{
    private static readonly DateTime Now = PremiFactory.Now;

    [Fact]
    public void Pay_SecondBeforeFirst_Throws()
    {
        var premi = PremiFactory.Create();

        var ex = Assert.Throws<DomainException>(() =>
            premi.MarkInstallmentPaid(PremiFactory.Nth(premi, 2).Id, Now));

        Assert.Equal("Previous installments must be paid first.", ex.Message);
    }

    [Fact]
    public void Pay_FirstTwiceManually_Throws()
    {
        var premi = PremiFactory.Create();
        var first = PremiFactory.Nth(premi, 1);
        premi.MarkInstallmentPaid(first.Id, Now);

        var ex = Assert.Throws<DomainException>(() => premi.MarkInstallmentPaid(first.Id, Now));

        Assert.Equal("Installment is already paid.", ex.Message);
    }

    [Fact]
    public void Pay_SameMidtransOrderTwice_IsIdempotent()
    {
        var premi = PremiFactory.Create();
        var first = PremiFactory.Nth(premi, 1);
        var one = premi.MarkInstallmentPaid(first.Id, Now, "CHK-1");
        var eventsAfterFirst = premi.DomainEvents.Count;

        var two = premi.MarkInstallmentPaid(first.Id, Now.AddMinutes(5), "CHK-1");

        Assert.Same(one, two);
        Assert.Equal(1, eventsAfterFirst);
        Assert.Equal(eventsAfterFirst, premi.DomainEvents.Count);
    }

    [Fact]
    public void Pay_DifferentMidtransOrderOnPaidInstallment_Throws()
    {
        var premi = PremiFactory.Create();
        var first = PremiFactory.Nth(premi, 1);
        premi.MarkInstallmentPaid(first.Id, Now, "CHK-1");

        var ex = Assert.Throws<DomainException>(() => premi.MarkInstallmentPaid(first.Id, Now, "CHK-2"));

        Assert.Equal("Installment is already paid.", ex.Message);
    }

    [Fact]
    public void Pay_All_CompletesPremi()
    {
        var premi = PremiFactory.Create();

        PremiFactory.PayAll(premi);

        Assert.Equal(PremiStatus.Completed, premi.Status);
        Assert.Equal(0m, premi.RemainingAmount);
        Assert.Null(premi.NextDueDate);
        Assert.Equal(3, premi.InstallmentsPaid);
    }

    [Fact]
    public void Pay_RaisesEvent_PremiCompletedOnlyOnLast()
    {
        var premi = PremiFactory.Create();

        PremiFactory.PayAll(premi);

        var events = premi.DomainEvents.OfType<InstallmentPaidEvent>().ToList();
        Assert.Equal(3, events.Count);
        Assert.False(events[0].PremiCompleted);
        Assert.False(events[1].PremiCompleted);
        Assert.True(events[2].PremiCompleted);
        Assert.Equal(3, events[2].InstallmentNumber);
        Assert.Equal(premi.Id, events[2].PremiId);
    }

    [Fact]
    public void Pay_CancelledPremi_Throws()
    {
        var premi = PremiFactory.Create();
        var first = PremiFactory.Nth(premi, 1);
        premi.Cancel();

        var ex = Assert.Throws<DomainException>(() => premi.MarkInstallmentPaid(first.Id, Now));

        Assert.Equal("Premi is not active.", ex.Message);
    }

    [Fact]
    public void Pay_UnknownInstallment_Throws()
    {
        var premi = PremiFactory.Create();

        var ex = Assert.Throws<DomainException>(() => premi.MarkInstallmentPaid(InstallmentId.New(), Now));

        Assert.Equal("Installment not found in this premi.", ex.Message);
    }

    [Fact]
    public void CheckCanPay_ReturnsEachRejectionReason()
    {
        var active = PremiFactory.Create();
        var first = PremiFactory.Nth(active, 1);
        var second = PremiFactory.Nth(active, 2);

        Assert.Null(active.CheckCanPay(first.Id));
        Assert.Equal(PaymentRejectionReason.PreviousInstallmentUnpaid, active.CheckCanPay(second.Id));
        Assert.Equal(PaymentRejectionReason.InstallmentNotFound, active.CheckCanPay(InstallmentId.New()));

        active.MarkInstallmentPaid(first.Id, Now);
        Assert.Equal(PaymentRejectionReason.InstallmentAlreadyPaid, active.CheckCanPay(first.Id));
        Assert.Null(active.CheckCanPay(second.Id));

        var cancelled = PremiFactory.Create();
        var id = PremiFactory.Nth(cancelled, 1).Id;
        cancelled.Cancel();
        Assert.Equal(PaymentRejectionReason.PremiNotActive, cancelled.CheckCanPay(id));
    }

    [Fact]
    public void NextUnpaidInstallment_FollowsPayments()
    {
        var premi = PremiFactory.Create();
        Assert.Equal(1, premi.NextUnpaidInstallment()!.InstallmentNumber);

        premi.MarkInstallmentPaid(PremiFactory.Nth(premi, 1).Id, Now);
        Assert.Equal(2, premi.NextUnpaidInstallment()!.InstallmentNumber);

        premi.MarkInstallmentPaid(PremiFactory.Nth(premi, 2).Id, Now);
        premi.MarkInstallmentPaid(PremiFactory.Nth(premi, 3).Id, Now);
        Assert.Null(premi.NextUnpaidInstallment());
    }

    [Fact]
    public void NextUnpaidInstallment_CancelledPremi_IsNull()
    {
        var premi = PremiFactory.Create();
        premi.Cancel();

        Assert.Null(premi.NextUnpaidInstallment());
    }
}
