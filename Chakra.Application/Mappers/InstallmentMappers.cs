using Chakra.Application.Features.Installments.Dtos;
using Chakra.Domain.Entities;
using Mapster;

namespace Chakra.Application.Mappers;

public static class InstallmentMappers
{
    public static InstallmentResponseDto ToResponseDto(this Installment installment) =>
        installment.Adapt<InstallmentResponseDto>()!;
}
