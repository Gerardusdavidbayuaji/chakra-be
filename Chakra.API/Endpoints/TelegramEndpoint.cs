using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Command;
using Chakra.Application.Features.Telegram.Dtos;

namespace Chakra.API.Endpoints;

public static class TelegramEndpoint
{
    public static void MapTelegramEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/telegram").WithTags("Telegram");

        group.MapPost("/link", [Authorize] async (IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateTelegramLinkCommand());
            return result.IsSuccess
                ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Telegram link created successfully"))
                : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
        })
        .WithName("Create Telegram Link");

        // Dipanggil oleh server Telegram. Selalu 200 untuk secret valid agar Telegram tidak mengirim ulang.
        group.MapPost("/webhook", async (
            [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
            TelegramUpdateDto update,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new ProcessTelegramWebhookCommand(secretToken, update));
            return result.IsSuccess ? Results.Ok() : Results.Unauthorized();
        })
        .WithName("Telegram Webhook");
    }
}
