using System.Security.Claims;
using Chakra.Application.Common;
using Chakra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chakra.API.Middlewares;

public class EnsureUserMiddleware
{
    private readonly RequestDelegate _next;

    public EnsureUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var supabaseAuthId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = context.User.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrEmpty(supabaseAuthId) && !string.IsNullOrEmpty(email))
            {
                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.SupabaseAuthId == supabaseAuthId || u.Email == email);

                if (user == null)
                {
                    var name = email.Contains('@') ? email[..email.IndexOf('@')] : email;

                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        Email = email,
                        SupabaseAuthId = supabaseAuthId,
                        ChatId = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    dbContext.Users.Add(user);

                    try
                    {
                        await dbContext.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // Race condition: another request already created this user
                        user = await dbContext.Users
                            .FirstOrDefaultAsync(u => u.SupabaseAuthId == supabaseAuthId || u.Email == email);
                    }
                }
                else if (user.SupabaseAuthId == null)
                {
                    // User exists by email but doesn't have SupabaseAuthId yet — link it
                    user.SupabaseAuthId = supabaseAuthId;
                    user.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }

                context.Items["CurrentUser"] = user;
            }
        }

        await _next(context);
    }
}
