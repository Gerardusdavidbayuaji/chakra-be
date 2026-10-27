using System.Text.Json;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

public class OutboxMessage : AuditableEntity<OutboxMessageId>
{
    public const int LastErrorMaxLength = 1000;
    
    public OutboxMessageType Type { get; set; } // jenis pesan, hanya informatif (worker memperlakukan semua tipe sama)
    public required string Payload { get; set; } // JSON berbentuk TelegramOutboxPayload
    public bool IsProcessed { get; set; } // true jika sudah berhasil terkirim
    public int RetryCount { get; set; } // jumlah percobaan gagal
    public string? LastError { get; set; } // pesan error percobaan terakhir
    public DateTime? ProcessedAt { get; set; } // waktu berhasil terkirim

    public static OutboxMessage CreateTelegram(OutboxMessageType type, UserId userId, string text) => new()
    {
        Id = OutboxMessageId.New(),
        Type = type,
        Payload = JsonSerializer.Serialize(new TelegramOutboxPayload(userId.Value, text)),
    };
    
    public void MarkAsProcessed(DateTime processedAt)
    {
        IsProcessed = true;
        ProcessedAt = processedAt;
        LastError = null;
    }
    
    public void MarkAsFailed(string error)
    {
        RetryCount++;
        LastError = Truncate(error);
    }
    
    // Untuk kegagalan yang tidak akan berhasil walau dicoba ulang (payload rusak, user belum link Telegram)
    public void MarkAsDeadLetter(string error, int maxRetries)
    {
        RetryCount = maxRetries;
        LastError = Truncate(error);
    }
    
    private static string Truncate(string error) =>
        error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
}

public sealed record TelegramOutboxPayload(Guid UserId, string Text);