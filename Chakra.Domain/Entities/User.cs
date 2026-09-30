using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Entities;

public class User : AuditableEntity<UserId>
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? SupabaseAuthId { get; set; }
    public Guid ChatId { get; set; }
}
