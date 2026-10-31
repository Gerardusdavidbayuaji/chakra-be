namespace Chakra.Application.Features.Telegram.Dtos;

public class TelegramLinkResponseDto
{
    public required string DeepLink { get; set; }
    public DateTime ExpiresAt { get; set; }
}
