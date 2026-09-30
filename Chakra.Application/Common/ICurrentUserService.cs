using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;

namespace Chakra.Application.Common;

public interface ICurrentUserService
{
    string? SupabaseAuthId { get; }
    string? Email { get; }
    UserId? DatabaseUserId { get; }
    User? GetCurrentUser();
}
