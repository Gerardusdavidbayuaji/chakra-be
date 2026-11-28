namespace Chakra.Application.Features.Midtrans.Dtos;

public record SnapRequest(
    string OrderId,
    long GrossAmount,
    string CustomerName,
    string CustomerEmail,
    string ItemName,
    int ExpiryHours);
