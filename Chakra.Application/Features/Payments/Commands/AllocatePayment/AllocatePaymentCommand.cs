using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;

namespace Chakra.Application.Features.Payments.Commands.AllocatePayment;

// Admin menerapkan dana yang ditampung ke cicilan berikutnya yang belum lunas
public record AllocatePaymentCommand(Guid PaymentId, string? Note) : IAdminRequest<Result<PaymentTransactionDto>>;
