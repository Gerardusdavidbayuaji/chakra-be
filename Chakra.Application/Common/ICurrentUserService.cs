using Chakra.Domain.Entities;

namespace Chakra.Application.Common;

public interface ICurrentUserService
{
    string? SupabaseAuthId { get; }
    string? Email { get; }
    Guid? DatabaseUserId { get; }
    User? GetCurrentUser();
}
