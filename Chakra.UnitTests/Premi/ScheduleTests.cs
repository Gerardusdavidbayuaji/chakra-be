using Chakra.Domain.Exceptions;
using Chakra.UnitTests.Support;

namespace Chakra.UnitTests.PremiScheduleTests;

public class ScheduleTests
{
    [Fact]
    public void Create_SpreadsRemainderOnLastInstallment()
    {
        var premi = PremiFactory.Create(total: 12_000_000m, tenor: 7);

        var amounts = premi.Installments.OrderBy(i => i.InstallmentNumber).Select(i => i.Amount).ToList();

        Assert.Equal(7, amounts.Count);
        Assert.All(amounts.Take(6), a => Assert.Equal(1_714_285m, a));
        Assert.Equal(1_714_290m, amounts[6]);
        Assert.Equal(12_000_000m, amounts.Sum());
    }

    [Fact]
    public void Create_SetsBaseInstallmentAmountToFloor()
    {
        var premi = PremiFactory.Create(total: 12_000_000m, tenor: 7);

        Assert.Equal(1_714_285m, premi.InstallmentAmount);
    }

    [Fact]
    public void Create_DueDay31_ClampsToMonthEndWithoutShrinkingAfterFebruary()
    {
        var premi = PremiFactory.Create(tenor: 3, dueDay: 31, start: new DateOnly(2027, 1, 31));

        var dues = premi.Installments.OrderBy(i => i.InstallmentNumber).Select(i => i.DueDate).ToList();

        Assert.Equal(new[]
        {
            new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31)
        }, dues);
    }

    [Fact]
    public void Create_DueDayOnStartDate_FirstDueIsThatMonth()
    {
        var premi = PremiFactory.Create(dueDay: 15, start: new DateOnly(2027, 1, 15));

        Assert.Equal(new DateOnly(2027, 1, 15), PremiFactory.Nth(premi, 1).DueDate);
    }

    [Fact]
    public void Create_DueDayAlreadyPassed_FirstDueIsNextMonth()
    {
        var premi = PremiFactory.Create(dueDay: 15, start: new DateOnly(2027, 1, 20));

        Assert.Equal(new DateOnly(2027, 2, 15), PremiFactory.Nth(premi, 1).DueDate);
    }

    [Fact]
    public void Create_FractionalTotal_Throws()
    {
        Assert.Throws<DomainException>(() => PremiFactory.Create(total: 1_000_000.50m));
    }

    [Fact]
    public void Create_TotalSmallerThanTenor_Throws()
    {
        Assert.Throws<DomainException>(() => PremiFactory.Create(total: 5m, tenor: 12));
    }
}
