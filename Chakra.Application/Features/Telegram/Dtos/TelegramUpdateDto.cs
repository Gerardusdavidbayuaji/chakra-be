using System.Text.Json.Serialization;

namespace Chakra.Application.Features.Telegram.Dtos;

public class TelegramUpdateDto
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }
}
