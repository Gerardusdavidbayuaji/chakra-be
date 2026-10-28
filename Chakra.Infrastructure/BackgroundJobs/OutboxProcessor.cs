using System.Text.Json;
using Chakra.Application.Common;
using Chakra.Application.Features.Outbox.Settings;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chakra.Infrastructure.BackgroundJobs;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxSettings _settings;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxSettings> settings,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.IntervalSeconds));

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
                    _logger.LogError(ex, "Outbox batch processing failed.");
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
        var telegramClient = scope.ServiceProvider.GetRequiredService<ITelegramClient>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED: baris yang sedang diproses instance lain dilewati, sehingga pesan tidak terkirim dobel
        var messages = await context.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "IsProcessed" = false AND "RetryCount" < {_settings.MaxRetries}
                ORDER BY "CreatedAt"
                LIMIT {_settings.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var payloads = messages.ToDictionary(m => m.Id, m => TryDeserialize(m.Payload));

        var userIds = payloads.Values
            .Where(p => p != null)
            .Select(p => new UserId(p!.UserId))
            .Distinct()
            .ToList();

        var chatIds = (await context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(u => u.Id.Value, u => u.TelegramChatId);

        foreach (var message in messages)
        {
            var payload = payloads[message.Id];
            if (payload == null)
            {
                message.MarkAsDeadLetter("Invalid payload.", _settings.MaxRetries);
                continue;
            }

            if (!chatIds.TryGetValue(payload.UserId, out var chatId) || chatId == null)
            {
                message.MarkAsDeadLetter("User not found or has not linked Telegram.", _settings.MaxRetries);
                continue;
            }

            try
            {
                await telegramClient.SendMessageAsync(chatId.Value, payload.Text, cancellationToken);
                message.MarkAsProcessed(timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.MarkAsFailed(ex.Message);
                _logger.LogWarning(ex, "Failed to send outbox message {OutboxMessageId} (attempt {RetryCount}).",
                    message.Id, message.RetryCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static TelegramOutboxPayload? TryDeserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<TelegramOutboxPayload>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
