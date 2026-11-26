using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Entities.Reminders;
using Chakra.UnitTests.Support;

namespace Chakra.UnitTests.PremiReminderTests;

public class ReminderTests
{
    private static readonly ReminderPolicy Policy = new(3, 1, 3, 27);

    private static DateOnly Jan(int day) => new(2027, 1, day);
    private static DateOnly Feb(int day) => new(2027, 2, day);

    // D = 15 Jan 2027, G = 7
    private static Premi NewPremi() => PremiFactory.Create(grace: 7, start: Jan(10));

    // Meniru satu run harian: pesan dicatat bila Kind != null, alert dicatat bila AdminAlertDue
    private static ReminderDecision Run(Premi premi, DateOnly date)
    {
        var decision = premi.EvaluateReminders(date, Policy);
        if (decision.Kind != null)
            premi.RecordReminderSent(decision.Target!.Id, date);
        if (decision.AdminAlertDue)
            premi.RecordOverdueAlertSent(decision.Target!.Id, date);
        return decision;
    }

    private static Dictionary<DateOnly, (ReminderDecision Decision, InstallmentStatus Status)> RunRange(
        Premi premi, DateOnly from, DateOnly to)
    {
        var result = new Dictionary<DateOnly, (ReminderDecision, InstallmentStatus)>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var decision = Run(premi, d);
            result[d] = (decision, PremiFactory.Nth(premi, 1).Status);
        }
        return result;
    }

    [Fact]
    public void WorkedExample_FollowsSpec()
    {
        var premi = NewPremi();

        var days = RunRange(premi, Jan(11), Feb(11));

        Assert.Null(days[Jan(11)].Decision.Kind);
        Assert.Equal(InstallmentStatus.Pending, days[Jan(11)].Status);

        Assert.Equal(ReminderKind.Upcoming, days[Jan(12)].Decision.Kind);
        Assert.Equal(InstallmentStatus.Reminder1, days[Jan(12)].Status);

        Assert.Null(days[Jan(13)].Decision.Kind);

        Assert.Equal(ReminderKind.DueTomorrow, days[Jan(14)].Decision.Kind);
        Assert.Equal(InstallmentStatus.Reminder2, days[Jan(14)].Status);

        Assert.Equal(ReminderKind.DueToday, days[Jan(15)].Decision.Kind);
        Assert.Null(days[Jan(16)].Decision.Kind);
        Assert.Null(days[Jan(17)].Decision.Kind);

        Assert.Equal(ReminderKind.GracePeriod, days[Jan(18)].Decision.Kind);
        Assert.Equal(ReminderKind.GracePeriod, days[Jan(21)].Decision.Kind);

        Assert.Null(days[Jan(22)].Decision.Kind);
        Assert.Equal(InstallmentStatus.Reminder2, days[Jan(22)].Status);

        Assert.Null(days[Jan(23)].Decision.Kind);
        Assert.Equal(InstallmentStatus.Overdue, days[Jan(23)].Status);

        Assert.Equal(ReminderKind.Overdue, days[Jan(24)].Decision.Kind);
        Assert.Equal(9, days[Jan(24)].Decision.DaysLate);
        Assert.Equal(InstallmentStatus.Overdue, days[Jan(24)].Status);

        Assert.Equal(ReminderKind.Overdue, days[Jan(27)].Decision.Kind);

        Assert.True(days[Feb(11)].Decision.AdminAlertDue);
        Assert.False(days[Feb(10)].Decision.AdminAlertDue);
        Assert.All(days.Where(kv => kv.Key < Feb(11)), kv => Assert.False(kv.Value.Decision.AdminAlertDue));
    }

    [Fact]
    public void AdminAlert_IsOnlyDueOnce()
    {
        var premi = NewPremi();
        RunRange(premi, Jan(11), Feb(11));

        var next = Run(premi, Feb(12));

        Assert.False(next.AdminAlertDue);
        Assert.NotNull(PremiFactory.Nth(premi, 1).OverdueAlertSentOn);
    }

    [Fact]
    public void SameDateTwice_SecondRunSendsNothing()
    {
        var premi = NewPremi();

        var first = Run(premi, Jan(12));
        var second = Run(premi, Jan(12));

        Assert.Equal(ReminderKind.Upcoming, first.Kind);
        Assert.Null(second.Kind);
        Assert.Equal(1, PremiFactory.Nth(premi, 1).ReminderCount);
    }

    [Fact]
    public void CatchUp_FreshPremiOn25Jan_SendsSingleOverdue()
    {
        var premi = NewPremi();

        var first = Run(premi, Jan(25));
        var next = Run(premi, Jan(26));

        Assert.Equal(ReminderKind.Overdue, first.Kind);
        Assert.Equal(10, first.DaysLate);
        Assert.Equal(InstallmentStatus.Overdue, PremiFactory.Nth(premi, 1).Status);
        Assert.Null(next.Kind);
    }

    [Fact]
    public void Paused_SendsNothingButStatusStillPromotes()
    {
        var premi = NewPremi();
        premi.PauseReminders(UserId.New(), PremiFactory.Now);

        var early = Run(premi, Jan(12));
        Assert.Null(early.Kind);
        Assert.False(early.AdminAlertDue);
        Assert.Equal(InstallmentStatus.Reminder1, PremiFactory.Nth(premi, 1).Status);

        var late = Run(premi, Feb(11));
        Assert.Null(late.Kind);
        Assert.False(late.AdminAlertDue);
        Assert.Equal(InstallmentStatus.Overdue, PremiFactory.Nth(premi, 1).Status);
        Assert.Null(PremiFactory.Nth(premi, 1).OverdueAlertSentOn);
    }

    [Fact]
    public void Resume_ThenSameDate_SendsMessage()
    {
        var premi = NewPremi();
        premi.PauseReminders(UserId.New(), PremiFactory.Now);
        Assert.Null(Run(premi, Jan(12)).Kind);

        premi.ResumeReminders();
        var decision = Run(premi, Jan(12));

        Assert.NotNull(decision.Kind);
        Assert.Equal(ReminderKind.Upcoming, decision.Kind);
    }

    [Fact]
    public void OnlyEarliestUnpaidInstallmentIsTargeted()
    {
        var premi = NewPremi();
        Assert.Equal(1, premi.EvaluateReminders(Jan(12), Policy).Target!.InstallmentNumber);

        premi.MarkInstallmentPaid(PremiFactory.Nth(premi, 1).Id, PremiFactory.Now);
        var decision = premi.EvaluateReminders(Feb(13), Policy);

        Assert.Equal(2, decision.Target!.InstallmentNumber);
        Assert.Equal(InstallmentStatus.Reminder1, PremiFactory.Nth(premi, 2).Status);
        Assert.Equal(InstallmentStatus.Pending, PremiFactory.Nth(premi, 3).Status);
    }

    [Fact]
    public void OtherInstallmentPastGrace_BecomesOverdueWithoutBeingTarget()
    {
        var premi = NewPremi();

        // 23 Feb: cicilan 2 (jatuh tempo 15 Feb, grace sampai 22 Feb) sudah lewat grace, cicilan 1 masih target
        var decision = premi.EvaluateReminders(new DateOnly(2027, 2, 23), Policy);

        Assert.Equal(1, decision.Target!.InstallmentNumber);
        Assert.Equal(InstallmentStatus.Overdue, PremiFactory.Nth(premi, 1).Status);
        Assert.Equal(InstallmentStatus.Overdue, PremiFactory.Nth(premi, 2).Status);
        Assert.Equal(InstallmentStatus.Pending, PremiFactory.Nth(premi, 3).Status);
    }

    [Fact]
    public void CancelledPremi_ReturnsNone()
    {
        var premi = NewPremi();
        premi.Cancel();

        var decision = premi.EvaluateReminders(Jan(12), Policy);

        Assert.Null(decision.Target);
        Assert.Null(decision.Kind);
        Assert.False(decision.AdminAlertDue);
    }
}
