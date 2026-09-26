using Chakra.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace Chakra.API.Endpoints;

public static class AuthEndpoint
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/me", [Authorize] (ICurrentUserService currentUser) =>
            {
                var user = currentUser.GetCurrentUser();
                if (user == null)
                    return Results.Unauthorized();

                return Results.Ok(new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.SupabaseAuthId,
                    user.ChatId,
                    user.CreatedAt
                });
            })
            .WithName("GetMe");
    }
}