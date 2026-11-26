using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;

namespace Chakra.UnitTests.SchedulerJobRunTests;

public class JobRunTests
{
    private static readonly DateTime Now = new(2027, 1, 12, 2, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Date = new(2027, 1, 12);

    private static JobRun NewRun() => JobRun.Start("DailyReminders", Date, Now);

    [Fact]
    public void Start_IsAttemptOneRunning()
    {
        var run = NewRun();

        Assert.Equal(1, run.Attempt);
        Assert.Equal(JobRunStatus.Running, run.Status);
        Assert.Equal("DailyReminders", run.JobName);
        Assert.Equal(Date, run.BusinessDate);
        Assert.Equal(Now, run.StartedAt);
    }

    [Fact]
    public void Complete_NoFailures_Succeeds()
    {
        var run = NewRun();

        run.Complete(Now.AddMinutes(1), 10, 4, 1, 2, 0);

        Assert.Equal(JobRunStatus.Succeeded, run.Status);
        Assert.Equal(10, run.PremisProcessed);
        Assert.Equal(4, run.RemindersQueued);
        Assert.Equal(1, run.AdminAlertsQueued);
        Assert.Equal(2, run.Skipped);
        Assert.NotNull(run.FinishedAt);
    }

    [Fact]
    public void Complete_WithFailures_Fails()
    {
        var run = NewRun();

        run.Complete(Now.AddMinutes(1), 10, 4, 1, 2, 3);

        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Equal(3, run.Failures);
    }

    [Fact]
    public void Restart_IncrementsAttemptAndResetsRun()
    {
        var run = NewRun();
        run.Fail(Now.AddMinutes(1), "boom");

        run.Restart(Now.AddHours(1));

        Assert.Equal(2, run.Attempt);
        Assert.Equal(JobRunStatus.Running, run.Status);
        Assert.Equal(Now.AddHours(1), run.StartedAt);
        Assert.Null(run.FinishedAt);
        Assert.Null(run.LastError);
        Assert.Equal(0, run.Failures);
    }

    [Fact]
    public void Fail_TruncatesLastErrorTo1000()
    {
        var run = NewRun();

        run.Fail(Now, new string('x', 2000));

        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Equal(1000, run.LastError!.Length);
    }

    [Fact]
    public void Fail_ShortError_IsKept()
    {
        var run = NewRun();

        run.Fail(Now, "boom");

        Assert.Equal("boom", run.LastError);
    }

    [Fact]
    public void IsStale_OnlyWhenRunningAndOlderThanOneHour()
    {
        var running = NewRun();
        Assert.True(running.IsStale(Now.AddHours(2)));
        Assert.False(running.IsStale(Now.AddMinutes(30)));

        var finished = NewRun();
        finished.Complete(Now, 0, 0, 0, 0, 0);
        Assert.False(finished.IsStale(Now.AddHours(2)));

        var failed = NewRun();
        failed.Fail(Now, "boom");
        Assert.False(failed.IsStale(Now.AddHours(2)));
    }
}
