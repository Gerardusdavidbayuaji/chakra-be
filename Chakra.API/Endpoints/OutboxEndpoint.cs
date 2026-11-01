using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Outbox.Command;

namespace Chakra.API.Endpoints;

public static class OutboxEndpoint
{
    // Hanya untuk uji coba worker di Development
    public static void MapOutboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/outbox").WithTags("Outbox (Development)");

        group.MapPost("/test", async (EnqueueTestOutboxMessageCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);

            if (!result.IsSuccess)
            {
                return result.Errors != null
                    ? Results.BadRequest(ApiErrorResponse.Validation(result.Error!, result.Errors, 400))
                    : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
            }

            return Results.Ok(ApiResponse<object>.Success(result.Data, 200, "Outbox message enqueued"));
        })
        .WithName("Enqueue Test Outbox Message");
    }
}
