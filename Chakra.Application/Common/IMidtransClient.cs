using Chakra.Application.Features.Midtrans.Dtos;

namespace Chakra.Application.Common;

public interface IMidtransClient
{
    Task<SnapResult> CreateSnapTransactionAsync(SnapRequest request, CancellationToken cancellationToken = default);
    Task<MidtransStatus?> GetStatusAsync(string orderId, CancellationToken cancellationToken = default);
    Task ExpireAsync(string orderId, CancellationToken cancellationToken = default);
}
