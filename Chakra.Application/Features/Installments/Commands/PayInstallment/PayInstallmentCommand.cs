using Chakra.Application.Common;
using Chakra.Application.Features.Installments.Dtos;

namespace Chakra.Application.Features.Installments.Commands.PayInstallment;

// Pembayaran manual oleh admin (misal pembayaran di luar sistem). PaidAt = waktu request diproses.
public record PayInstallmentCommand(Guid InstallmentId) : IAdminRequest<Result<InstallmentResponseDto>>;
