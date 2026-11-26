using Chakra.Application.Common;
using Chakra.Application.Features.Midtrans.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chakra.Infrastructure.BackgroundJobs;

public class PaymentExpiryProcessor : BackgroundService
{
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MidtransSettings _settings;
    private readonly ILogger<PaymentExpiryProcessor> _logger;

    public PaymentExpiryProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<MidtransSettings> settings,
        ILogger<PaymentExpiryProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.ExpiryProcessorIntervalSeconds));

        try
        {
            do
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Payment expiry processing failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Aplikasi berhenti
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var midtransClient = scope.ServiceProvider.GetRequiredService<IMidtransClient>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        // Transaksi masih Pending 1 jam setelah ExpiresAt: Midtrans sudah pasti kedaluwarsa, cukup tandai lokal
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED: baris yang sedang diproses instance lain dilewati
        // xmin ditulis eksplisit karena SELECT * tidak menyertakan kolom sistem, sedangkan RowVersion dipetakan ke xmin
        var payments = await context.PaymentTransactions
            .FromSql($"""
                SELECT *, xmin FROM "PaymentTransactions"
                WHERE "Status" = 'Pending' AND ("ExpireRequested" = true OR "ExpiresAt" < {cutoff})
                ORDER BY "CreatedAt"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        foreach (var payment in payments)
        {
            if (payment.ExpireRequested)
            {
                try
                {
                    await midtransClient.ExpireAsync(payment.OrderId, cancellationToken);
                    payment.MarkExpired();
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    // Tetap Pending, dicoba lagi di siklus berikutnya
                    _logger.LogWarning(ex, "Failed to expire Midtrans transaction {OrderId}.", payment.OrderId);
                }
            }
            else
            {
                payment.MarkExpired();
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
