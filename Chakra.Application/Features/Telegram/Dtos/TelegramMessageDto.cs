using System.Text.Json.Serialization;

namespace Chakra.Application.Features.Telegram.Dtos;

public class TelegramMessageDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChatDto? Chat { get; set; }
}
