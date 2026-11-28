namespace Chakra.Application.Features.Midtrans.Dtos;

public record MidtransStatus(
    string OrderId,
    string StatusCode,
    string TransactionStatus,
    string? FraudStatus,
    string GrossAmount,
    string? PaymentType,
    string? TransactionId,
    DateTime? SettlementTimeUtc);
