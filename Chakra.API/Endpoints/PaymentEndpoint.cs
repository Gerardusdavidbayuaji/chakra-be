using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Command;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Application.Features.Payments.Queries;

namespace Chakra.API.Endpoints;

public static class PaymentEndpoint
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments").RequireAuthorization();

        // Admin: dana yang ditampung karena tidak bisa diterapkan otomatis
        group.MapGet("/unapplied", async (IMediator mediator, [AsParameters] GetUnappliedPaymentsQuery query) =>
            {
                var result = await mediator.Send(query);
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Data retrieved successfully"))
                    : result.ToErrorResult();
            })
            .WithName("Get Unapplied Payments");

        group.MapPost("/{id:guid}/allocate", async (Guid id, AllocatePaymentRequest? body, IMediator mediator) =>
            {
                var result = await mediator.Send(new AllocatePaymentCommand(id, body?.Note));
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Payment allocated successfully"))
                    : result.ToErrorResult();
            })
            .WithName("Allocate Payment");

        group.MapPost("/{id:guid}/refund", async (Guid id, RefundPaymentRequest body, IMediator mediator) =>
            {
                var result = await mediator.Send(new RefundPaymentCommand(id, body.Reference, body.Note));
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Payment refunded successfully"))
                    : result.ToErrorResult();
            })
            .WithName("Refund Payment");
    }
}
