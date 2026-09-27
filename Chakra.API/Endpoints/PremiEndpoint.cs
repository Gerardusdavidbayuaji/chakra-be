using MediatR;

using Chakra.Application.Features.Premi.Command;
using Chakra.Application.Features.Premi.Queries;
using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.API.Endpoints;

public static class PremiEndpoint
{
    public static void MapPremiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/premi").WithTags("Premi");

        group.MapGet("/", async (IMediator mediator, [AsParameters] GetAllPremiQuery query) =>
        {
            var result = await mediator.Send(query);
            return result.IsSuccess
                ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Data retrieved successfully"))
                : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
        })
        .WithName("Get All Premi");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPremiByIdQuery(id));
            return result.IsSuccess
                ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Data retrieved successfully"))
                : Results.NotFound(ApiErrorResponse.Error(result.Error!, 404));
        })
        .WithName("Get Premi By Id");

        group.MapPost("/", async (CreatePremiRequestDto dto, IMediator mediator) =>
        {
            var request = new CreatePremiInput
            {
                UserId = dto.UserId,
                TotalAmount = dto.TotalAmount,
                Tenor = dto.Tenor,
                DueDay = dto.DueDay,
                GracePeriodDays = dto.GracePeriodDays,
                StartDate = dto.StartDate
            };
            var result = await mediator.Send(request);

            if (!result.IsSuccess)
            {
                return result.Errors != null
                    ? Results.BadRequest(ApiErrorResponse.Validation(result.Error!, result.Errors, 400))
                    : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
            }

            return Results.Created(
                $"/api/premi/{result.Data!.Id}",
                ApiResponse<object>.Success(result.Data, 201, "Data created successfully"));
        })
        .WithName("Create Premi");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePremiRequestDto dto, IMediator mediator) =>
        {
            var request = new UpdatePremiInput
            {
                Id = id,
                TotalAmount = dto.TotalAmount,
                Tenor = dto.Tenor,
                DueDay = dto.DueDay,
                GracePeriodDays = dto.GracePeriodDays,
                StartDate = dto.StartDate
            };
            var result = await mediator.Send(request);

            return result.IsSuccess
                ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Data updated successfully"))
                : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
        })
        .WithName("Update Premi");

        group.MapPatch("/{id:guid}/cancel", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new CancelPremiRequest(id));
            return result.IsSuccess
                ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Premi cancelled successfully"))
                : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400));
        })
        .WithName("Cancel Premi");
    }
}
