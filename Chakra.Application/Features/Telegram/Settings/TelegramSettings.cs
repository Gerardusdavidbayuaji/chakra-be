namespace Chakra.Application.Features.Telegram.Settings;

public class TelegramSettings
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int LinkTokenLifetimeMinutes { get; set; } = 15;
}
