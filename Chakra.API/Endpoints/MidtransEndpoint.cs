using MediatR;

using Chakra.Application.Features.Midtrans.Dtos;
using Chakra.Application.Features.Midtrans.Commands.ProcessMidtransNotification;

namespace Chakra.API.Endpoints;

public static class MidtransEndpoint
{
    public static void MapMidtransEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/midtrans").WithTags("Midtrans");

        // Dipanggil oleh server Midtrans (anonymous). Keaslian dicek lewat signature_key di handler.
        group.MapPost("/notification", async (MidtransNotificationDto notification, IMediator mediator) =>
        {
            var result = await mediator.Send(new ProcessMidtransNotificationCommand(notification));
            return result.IsSuccess ? Results.Ok() : result.ToErrorResult();
        })
        .WithName("Midtrans Notification");
    }
}
