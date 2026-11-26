using Chakra.Application.Features.Payments.Dtos;
using Chakra.Domain.Entities;

namespace Chakra.Application.Mappers;

public static class PaymentMapper
{
    public static PaymentTransactionDto ToDto(this PaymentTransaction t)
    {
        var dto = new PaymentTransactionDto();
        t.CopyTo(dto);
        return dto;
    }

    public static void CopyTo(this PaymentTransaction t, PaymentTransactionDto dto)
    {
        dto.Id = t.Id.Value;
        dto.OrderId = t.OrderId;
        dto.InstallmentId = t.InstallmentId.Value;
        dto.PremiId = t.PremiId.Value;
        dto.Amount = t.Amount;
        dto.Status = t.Status;
        dto.PaymentType = t.PaymentType;
        dto.SettledAt = t.SettledAt;
        dto.RejectionReason = t.RejectionReason;
        dto.AllocatedInstallmentId = t.AllocatedInstallmentId?.Value;
        dto.RefundReference = t.RefundReference;
        dto.ResolutionNote = t.ResolutionNote;
        dto.ResolvedAt = t.ResolvedAt;
    }
}
