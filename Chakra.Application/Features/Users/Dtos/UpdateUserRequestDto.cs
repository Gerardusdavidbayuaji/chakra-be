namespace Chakra.Application.Features.Users.Dtos;

public class UpdateUserRequestDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}
