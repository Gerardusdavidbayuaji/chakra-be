using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Payments.Dtos;

public class PaymentTransactionDto
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public Guid InstallmentId { get; set; }
    public Guid PremiId { get; set; }
    public decimal Amount { get; set; }
    public PaymentTransactionStatus Status { get; set; }
    public string? PaymentType { get; set; }
    public DateTime? SettledAt { get; set; }
    public PaymentRejectionReason? RejectionReason { get; set; }
    public Guid? AllocatedInstallmentId { get; set; }
    public string? RefundReference { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
