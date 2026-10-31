using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Entities;

public class User : AuditableEntity<UserId>
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? SupabaseAuthId { get; set; }
    public long? TelegramChatId { get; private set; } // diisi saat user menekan Start di bot lewat deep link
    public string? TelegramLinkTokenHash { get; private set; } // SHA-256 hex dari token deep link, token asli tidak disimpan
    public DateTime? TelegramLinkTokenExpiresAt { get; private set; }

    public void SetTelegramLinkToken(string tokenHash, DateTime expiresAt)
    {
        TelegramLinkTokenHash = tokenHash;
        TelegramLinkTokenExpiresAt = expiresAt;
    }

    public void LinkTelegram(long chatId)
    {
        TelegramChatId = chatId;
        TelegramLinkTokenHash = null;
        TelegramLinkTokenExpiresAt = null;
    }
}