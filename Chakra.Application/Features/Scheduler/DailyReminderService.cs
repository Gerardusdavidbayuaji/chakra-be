using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Chakra.Application.Common;
using Chakra.Application.Features.Scheduler.Settings;
using Chakra.Application.Services;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Entities.Reminders;

namespace Chakra.Application.Features.Scheduler;

public sealed record DailyReminderResult(
    int PremisProcessed, int RemindersQueued, int AdminAlertsQueued, int Skipped, int Failures);

// Dipanggil worker (dan nanti pemicu lain). Satu scope DI + DbContext per premi
public class DailyReminderService
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SchedulerSettings _settings;
    private readonly ILogger<DailyReminderService> _logger;

    public DailyReminderService(
        IApplicationDbContext context,
        IServiceScopeFactory scopeFactory,
        IOptions<SchedulerSettings> settings,
        ILogger<DailyReminderService> logger)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<DailyReminderResult> RunAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        var policy = _settings.ToPolicy();
        var premiIds = await GetCandidateIdsAsync(businessDate, policy.DaysBeforeFirst, cancellationToken);

        var processed = 0;
        var remindersQueued = 0;
        var alertsQueued = 0;
        var skipped = 0;
        var failures = 0;

        foreach (var premiId in premiIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            var outcome = await ProcessPremiAsync(premiId, businessDate, policy, cancellationToken);
            remindersQueued += outcome.RemindersQueued;
            alertsQueued += outcome.AdminAlertsQueued;
            skipped += outcome.Skipped;
            failures += outcome.Failures;
        }

        _logger.LogInformation(
            "Daily reminders for {BusinessDate}: processed {Processed}, reminders {Reminders}, admin alerts {Alerts}, skipped {Skipped}, failures {Failures}",
            businessDate, processed, remindersQueued, alertsQueued, skipped, failures);

        return new DailyReminderResult(processed, remindersQueued, alertsQueued, skipped, failures);
    }

    // Id kandidat dibaca halaman demi halaman (offset paging, BatchSize per halaman, urut Id) sebelum pemrosesan dimulai,
    // sehingga premi yang berubah selama pemrosesan tidak menggeser halaman
    private async Task<List<PremiId>> GetCandidateIdsAsync(
        DateOnly businessDate, int daysBeforeFirst, CancellationToken cancellationToken)
    {
        var cutoff = businessDate.AddDays(daysBeforeFirst);
        var batchSize = Math.Max(1, _settings.BatchSize);
        var ids = new List<PremiId>();

        while (true)
        {
            var page = await _context.Premis
                .AsNoTracking()
                .Where(p => p.Status == PremiStatus.Active
                            && p.Installments.Any(i => i.Status != InstallmentStatus.Paid
                                                       && i.Status != InstallmentStatus.Cancelled
                                                       && i.DueDate <= cutoff))
                .OrderBy(p => p.Id)
                .Skip(ids.Count)
                .Take(batchSize)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            ids.AddRange(page);

            if (page.Count < batchSize)
                return ids;
        }
    }

    private readonly record struct PremiOutcome(int RemindersQueued, int AdminAlertsQueued, int Skipped, int Failures);

    private async Task<PremiOutcome> ProcessPremiAsync(
        PremiId premiId, DateOnly businessDate, ReminderPolicy policy,
        CancellationToken cancellationToken)
    {
        var remindersQueued = 0;
        var alertsQueued = 0;
        var skipped = 0;
        var failures = 0;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var paymentLinks = scope.ServiceProvider.GetRequiredService<IPaymentLinkService>();

            var premi = await context.Premis
                .Include(p => p.Installments)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);

            if (premi == null)
                return new PremiOutcome(0, 0, 0, 0);

            var decision = premi.EvaluateReminders(businessDate, policy);
            var target = decision.Target;

            if (decision.Kind != null && target != null)
            {
                if (premi.User.TelegramChatId == null)
                {
                    _logger.LogInformation(
                        "Premi {PremiId}: user has no linked Telegram, reminder skipped", premiId);
                    skipped++;
                }
                else if (!await IsStillPayableAsync(context, premi.Id, target.Id, cancellationToken))
                {
                    // Dibayar/dibatalkan di tengah run: tidak ada link dan tidak ada pesan
                    _logger.LogWarning(
                        "Premi {PremiId}: installment {InstallmentId} is no longer payable, reminder skipped",
                        premiId, target.Id);
                    skipped++;
                }
                else
                {
                    var link = await paymentLinks.GetOrCreateAsync(target.Id, cancellationToken);

                    if (link.IsSuccess && link.Data != null)
                    {
                        var text = ReminderMessages.ForCustomer(decision, link.Data.RedirectUrl, businessDate);
                        context.OutboxMessages.Add(
                            OutboxMessage.CreateTelegram(OutboxMessageType.TelegramReminder, premi.UserId, text));
                        premi.RecordReminderSent(target.Id, businessDate);
                        remindersQueued++;
                    }
                    else if (link.ErrorType == ErrorType.Unavailable)
                    {
                        _logger.LogWarning(
                            "Premi {PremiId}: payment link unavailable ({Error}), will retry", premiId, link.Error);
                        failures++;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Premi {PremiId}: payment link not created ({Error}), reminder skipped",
                            premiId, link.Error);
                        skipped++;
                    }
                }
            }

            if (decision.AdminAlertDue && target != null)
            {
                var admins = await context.Users
                    .Where(u => u.Role == UserRole.Admin && u.TelegramChatId != null)
                    .ToListAsync(cancellationToken);

                if (admins.Count == 0)
                {
                    _logger.LogWarning(
                        "Premi {PremiId}: overdue alert due but no admin has linked Telegram", premiId);
                }
                else
                {
                    var text = ReminderMessages.ForAdmin(premi.User, target);
                    foreach (var admin in admins)
                        context.OutboxMessages.Add(
                            OutboxMessage.CreateTelegram(OutboxMessageType.AdminAlert, admin.Id, text));

                    premi.RecordOverdueAlertSent(target.Id, businessDate);
                    alertsQueued++;
                }
            }

            // Selalu disimpan agar perubahan status tersimpan walau tidak ada pesan
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Premi {PremiId}: concurrent change detected, will be re-evaluated", premiId);
            return new PremiOutcome(0, 0, 0, 1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Premi {PremiId}: failed to process reminders", premiId);
            return new PremiOutcome(0, 0, 0, 1);
        }

        return new PremiOutcome(remindersQueued, alertsQueued, skipped, failures);
    }

    // Baca ulang status dari database (tanpa tracking) karena entitas premi di scope ini bisa sudah usang
    private static async Task<bool> IsStillPayableAsync(
        IApplicationDbContext context, PremiId premiId, InstallmentId installmentId, CancellationToken cancellationToken)
    {
        var premiActive = await context.Premis
            .AsNoTracking()
            .AnyAsync(p => p.Id == premiId && p.Status == PremiStatus.Active, cancellationToken);
        if (!premiActive)
            return false;

        return await context.Installments
            .AsNoTracking()
            .AnyAsync(i => i.Id == installmentId
                           && i.Status != InstallmentStatus.Paid
                           && i.Status != InstallmentStatus.Cancelled, cancellationToken);
    }
}
