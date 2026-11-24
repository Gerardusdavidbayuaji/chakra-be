using Chakra.Application.Common;
using Chakra.Application.Features.Scheduler;

namespace Chakra.API.Endpoints;

public static class JobEndpoint
{
    // Hanya untuk uji coba jadwal pengingat di Development
    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs")
            .WithTags("Jobs (Development)")
            .RequireAuthorization();

        group.MapPost("/daily-reminders/run", async (
            DateOnly? date,
            DailyReminderService service,
            ICurrentUserService currentUser,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.IsAdmin)
                return Results.Json(ApiErrorResponse.Error("Forbidden", 403), statusCode: 403);

            if (date is null)
                return Results.BadRequest(ApiErrorResponse.Error("Query parameter 'date' (yyyy-MM-dd) is required.", 400));

            var result = await service.RunAsync(date.Value, cancellationToken);

            return Results.Ok(ApiResponse<object>.Success(result, 200, "Daily reminders executed"));
        })
        .WithName("Run Daily Reminders");
    }
}
