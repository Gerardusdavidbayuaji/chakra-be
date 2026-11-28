using System.Text.Json.Serialization;

namespace Chakra.Application.Features.Telegram.Dtos;

public class TelegramChatDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}
