using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Events;
using Chakra.Domain.Exceptions;
using Chakra.UnitTests.Support;

namespace Chakra.UnitTests.PremiCancelTests;

public class CancelTests
{
    [Fact]
    public void Cancel_KeepsPaidAndCancelsOthers()
    {
        var premi = PremiFactory.Create();
        premi.MarkInstallmentPaid(PremiFactory.Nth(premi, 1).Id, PremiFactory.Now);

        premi.Cancel();

        Assert.Equal(PremiStatus.Cancelled, premi.Status);
        Assert.Equal(InstallmentStatus.Paid, PremiFactory.Nth(premi, 1).Status);
        Assert.Equal(InstallmentStatus.Cancelled, PremiFactory.Nth(premi, 2).Status);
        Assert.Equal(InstallmentStatus.Cancelled, PremiFactory.Nth(premi, 3).Status);
    }

    [Fact]
    public void Cancel_Twice_Throws()
    {
        var premi = PremiFactory.Create();
        premi.Cancel();

        var ex = Assert.Throws<DomainException>(() => premi.Cancel());

        Assert.Equal("Premi is already cancelled.", ex.Message);
    }

    [Fact]
    public void Cancel_Completed_Throws()
    {
        var premi = PremiFactory.Create();
        PremiFactory.PayAll(premi);

        var ex = Assert.Throws<DomainException>(() => premi.Cancel());

        Assert.Equal("Completed premi cannot be cancelled.", ex.Message);
    }

    [Fact]
    public void Cancel_RaisesPremiCancelledEvent()
    {
        var premi = PremiFactory.Create();

        premi.Cancel();

        var evt = Assert.Single(premi.DomainEvents.OfType<PremiCancelledEvent>());
        Assert.Equal(premi.Id, evt.PremiId);
    }
}
