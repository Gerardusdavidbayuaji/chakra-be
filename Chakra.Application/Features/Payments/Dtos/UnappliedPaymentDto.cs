using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Payments.Dtos;

public class UnappliedPaymentDto : PaymentTransactionDto
{
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int InstallmentNumber { get; set; }
    public PremiStatus PremiStatus { get; set; }
    public bool CanAllocate { get; set; }
}
