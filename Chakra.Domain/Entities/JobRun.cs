using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

// Jejak audit satu run job per (JobName, BusinessDate)
public class JobRun : AuditableEntity<JobRunId>
{
    public const int LastErrorMaxLength = 1000;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    public string JobName { get; private set; } = null!;
    public DateOnly BusinessDate { get; private set; } // tanggal di zona bisnis yang diproses
    public JobRunStatus Status { get; private set; }
    public int Attempt { get; private set; }
    public DateTime StartedAt { get; private set; } // UTC
    public DateTime? FinishedAt { get; private set; } // UTC
    public int PremisProcessed { get; private set; }
    public int RemindersQueued { get; private set; }
    public int AdminAlertsQueued { get; private set; }
    public int Skipped { get; private set; }
    public int Failures { get; private set; }
    public string? LastError { get; private set; }
    public uint RowVersion { get; private set; }

    private JobRun()
    {
    }

    public static JobRun Start(string jobName, DateOnly businessDate, DateTime now) => new()
    {
        Id = JobRunId.New(),
        JobName = jobName,
        BusinessDate = businessDate,
        Status = JobRunStatus.Running,
        Attempt = 1,
        StartedAt = now
    };

    // Klaim ulang run yang Failed (atau Running yang tertinggal)
    public void Restart(DateTime now)
    {
        Attempt++;
        Status = JobRunStatus.Running;
        StartedAt = now;
        FinishedAt = null;
        PremisProcessed = 0;
        RemindersQueued = 0;
        AdminAlertsQueued = 0;
        Skipped = 0;
        Failures = 0;
        LastError = null;
    }

    public void Complete(DateTime now, int premisProcessed, int remindersQueued, int adminAlertsQueued,
        int skipped, int failures)
    {
        PremisProcessed = premisProcessed;
        RemindersQueued = remindersQueued;
        AdminAlertsQueued = adminAlertsQueued;
        Skipped = skipped;
        Failures = failures;
        FinishedAt = now;
        Status = failures == 0 ? JobRunStatus.Succeeded : JobRunStatus.Failed;
    }

    public void Fail(DateTime now, string error)
    {
        Status = JobRunStatus.Failed;
        FinishedAt = now;
        LastError = error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
    }

    public bool IsStale(DateTime now) =>
        Status == JobRunStatus.Running && StartedAt < now - StaleAfter;
}
