using System.Globalization;
using Chakra.Application.Features.Scheduler;
using Chakra.Application.Features.Scheduler.Settings;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chakra.Infrastructure.BackgroundJobs;

// Memeriksa berkala apakah job pengingat harian untuk tanggal bisnis berjalan sudah harus dijalankan.
// Satu JobRun per (JobName, BusinessDate) menjadi klaim lintas instance
public class DailyReminderProcessor : BackgroundService
{
    private static readonly TimeSpan MinutesModeInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly SchedulerSettings _settings;
    private readonly ILogger<DailyReminderProcessor> _logger;
    private readonly TimeZoneInfo _timeZone;
    private readonly int _maxAttempts;
    private readonly TimeOnly _runAtLocalTime;

    private DateTimeOffset _processStartedAt;

    public DailyReminderProcessor(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<SchedulerSettings> settings,
        ILogger<DailyReminderProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _settings = settings.Value;
        _logger = logger;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZone);
        _maxAttempts = Math.Max(1, _settings.MaxAttempts);
        _runAtLocalTime = TimeOnly.Parse(_settings.RunAtLocalTime, CultureInfo.InvariantCulture);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processStartedAt = _timeProvider.GetUtcNow();

        var interval = _settings.TimeScale == SchedulerTimeScale.Minutes
            ? MinutesModeInterval
            : TimeSpan.FromMinutes(Math.Max(1, _settings.CheckIntervalMinutes));

        using var timer = new PeriodicTimer(interval);

        try
        {
            do
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Daily reminder cycle failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Aplikasi berhenti
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var businessDate = ResolveBusinessDate(now);
        if (businessDate == null)
            return;

        var utcNow = now.UtcDateTime;
        var jobName = SchedulerSettings.DailyRemindersJobName;

        // Scope + DbContext tersendiri untuk pembukuan JobRun, terpisah dari scope per premi di DailyReminderService
        await using var bookkeepingScope = _scopeFactory.CreateAsyncScope();
        var context = bookkeepingScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var jobRun = await context.JobRuns
            .FirstOrDefaultAsync(j => j.JobName == jobName && j.BusinessDate == businessDate.Value, cancellationToken);

        if (jobRun == null)
        {
            jobRun = JobRun.Start(jobName, businessDate.Value, utcNow);
            context.JobRuns.Add(jobRun);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                // Unique (JobName, BusinessDate): instance lain sudah mengklaim
                _logger.LogInformation("Daily reminders for {BusinessDate} already claimed by another instance.",
                    businessDate.Value);
                return;
            }
        }
        else if ((jobRun.Status == JobRunStatus.Failed || jobRun.IsStale(utcNow)) && jobRun.Attempt < _maxAttempts)
        {
            jobRun.Restart(utcNow);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogInformation("Daily reminders for {BusinessDate} already re-claimed by another instance.",
                    businessDate.Value);
                return;
            }
        }
        else
        {
            return;
        }

        try
        {
            await using var runScope = _scopeFactory.CreateAsyncScope();
            var service = runScope.ServiceProvider.GetRequiredService<DailyReminderService>();
            var result = await service.RunAsync(businessDate.Value, cancellationToken);

            jobRun.Complete(_timeProvider.GetUtcNow().UtcDateTime, result.PremisProcessed, result.RemindersQueued,
                result.AdminAlertsQueued, result.Skipped, result.Failures);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Daily reminders for {BusinessDate} failed (attempt {Attempt}).",
                businessDate.Value, jobRun.Attempt);
            jobRun.Fail(_timeProvider.GetUtcNow().UtcDateTime, ex.Message);
        }

        if (jobRun.Status == JobRunStatus.Failed && jobRun.Attempt >= _maxAttempts)
            await QueueFinalFailureAlertAsync(context, jobRun, businessDate.Value, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task QueueFinalFailureAlertAsync(
        ApplicationDbContext context, JobRun jobRun, DateOnly businessDate, CancellationToken cancellationToken)
    {
        var admins = await context.Users
            .Where(u => u.Role == UserRole.Admin && u.TelegramChatId != null)
            .ToListAsync(cancellationToken);

        if (admins.Count == 0)
        {
            _logger.LogWarning("Daily reminders for {BusinessDate} failed permanently but no admin has linked Telegram.",
                businessDate);
            return;
        }

        var text = $"Job pengingat harian {businessDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("id-ID"))} " +
                   $"gagal setelah {jobRun.Attempt} percobaan: " +
                   $"{jobRun.LastError ?? $"{jobRun.Failures} premi gagal diproses"}";

        foreach (var admin in admins)
            context.OutboxMessages.Add(OutboxMessage.CreateTelegram(OutboxMessageType.AdminAlert, admin.Id, text));
    }

    // null = belum waktunya menjalankan job hari ini (mode Days)
    private DateOnly? ResolveBusinessDate(DateTimeOffset now)
    {
        if (_settings.TimeScale == SchedulerTimeScale.Minutes)
        {
            var elapsedDays = (int)Math.Floor((now - _processStartedAt).TotalMinutes);
            return _settings.SimulationStartDate!.Value.AddDays(elapsedDays);
        }

        var local = TimeZoneInfo.ConvertTime(now, _timeZone);
        if (TimeOnly.FromDateTime(local.DateTime) < _runAtLocalTime)
            return null;

        return DateOnly.FromDateTime(local.DateTime);
    }
}
