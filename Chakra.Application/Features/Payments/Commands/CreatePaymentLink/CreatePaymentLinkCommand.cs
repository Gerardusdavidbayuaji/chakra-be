using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;

namespace Chakra.Application.Features.Payments.Commands.CreatePaymentLink;

public record CreatePaymentLinkCommand(Guid InstallmentId) : IAuthorizedRequest<Result<PaymentLinkDto>>;
