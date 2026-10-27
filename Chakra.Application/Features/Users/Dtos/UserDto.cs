namespace Chakra.Application.Features.Users.Dtos;

public class UpdateUserRequestDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}

public class UserResponseDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; } = string.Empty;
    public required string Email { get; set; } = string.Empty;
    public long? TelegramChatId { get; set; }
    public DateTime CreatedAt { get; set; }
}

