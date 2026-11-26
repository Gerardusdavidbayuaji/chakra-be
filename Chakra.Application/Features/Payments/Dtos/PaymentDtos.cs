using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Payments.Dtos;

public class PaymentLinkDto
{
    public string OrderId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string SnapToken { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpiresAt { get; set; }
}

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

public class UnappliedPaymentDto : PaymentTransactionDto
{
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int InstallmentNumber { get; set; }
    public PremiStatus PremiStatus { get; set; }
    public bool CanAllocate { get; set; }
}

public record AllocatePaymentRequest(string? Note);

public record RefundPaymentRequest(string Reference, string? Note);
