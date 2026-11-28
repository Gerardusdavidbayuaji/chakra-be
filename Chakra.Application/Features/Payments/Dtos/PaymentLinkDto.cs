namespace Chakra.Application.Features.Payments.Dtos;

public class PaymentLinkDto
{
    public string OrderId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string SnapToken { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpiresAt { get; set; }
}
