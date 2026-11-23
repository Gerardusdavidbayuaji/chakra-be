using Chakra.Application.Common;
using Chakra.Application.Features.Installments.Command;
using Chakra.Application.Features.Installments.Queries;
using Chakra.Application.Features.Payments.Command;
using MediatR;

namespace Chakra.API.Endpoints;

public static class InstallmentEndpoint
{
    public static void MapInstallmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("Installments").RequireAuthorization();

        group.MapGet("/premi/{premiId:guid}/installments", async (Guid premiId, IMediator mediator,
                [AsParameters] GetAllInstallmentByPremiIdQuery query) =>
            {
                query.PremiId = premiId;
                var result = await mediator.Send(query);
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Data retrieved successfully"))
                    : result.ToErrorResult();
            })
            .WithName("Get All Installment By PremiId");

        group.MapGet("/installments/{id:guid}", async (Guid id, IMediator mediator) =>
            {
                var result = await mediator.Send(new GetInstallmentByIdQuery(id));
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Data retrieved successfully"))
                    : result.ToErrorResult();
            })
            .WithName("GetInstallmentById");

        // Admin: tandai cicilan lunas secara manual
        group.MapPost("/installments/{id:guid}/pay", async (Guid id, IMediator mediator) =>
            {
                var result = await mediator.Send(new PayInstallmentCommand(id));
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Installment paid successfully"))
                    : result.ToErrorResult();
            })
            .WithName("Pay Installment");

        // Pemilik atau admin: buat (atau pakai ulang) link pembayaran Midtrans
        group.MapPost("/installments/{id:guid}/payment-link", async (Guid id, IMediator mediator) =>
            {
                var result = await mediator.Send(new CreatePaymentLinkCommand(id));
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Payment link created successfully"))
                    : result.ToErrorResult();
            })
            .WithName("Create Payment Link");
    }
}
