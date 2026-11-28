using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;

namespace Chakra.Application.Features.Payments.Commands.RefundPayment;

// Admin mencatat bahwa dana yang ditampung sudah dikembalikan manual (di luar sistem)
public record RefundPaymentCommand(Guid PaymentId, string Reference, string? Note) : IAdminRequest<Result<PaymentTransactionDto>>;
