using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Domain.Entities.Common;

namespace Chakra.Application.Services;

public interface IPaymentLinkService
{
    // Tidak mengecek kepemilikan; pemanggil yang bertanggung jawab
    Task<Result<PaymentLinkDto>> GetOrCreateAsync(InstallmentId installmentId, CancellationToken cancellationToken);
}
