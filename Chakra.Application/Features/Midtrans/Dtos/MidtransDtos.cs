using System.Text.Json.Serialization;

namespace Chakra.Application.Features.Midtrans.Dtos;

public record SnapRequest(
    string OrderId,
    long GrossAmount,
    string CustomerName,
    string CustomerEmail,
    string ItemName,
    int ExpiryHours);

public record SnapResult(string Token, string RedirectUrl);

public record MidtransStatus(
    string OrderId,
    string StatusCode,
    string TransactionStatus,
    string? FraudStatus,
    string GrossAmount,
    string? PaymentType,
    string? TransactionId,
    DateTime? SettlementTimeUtc);

// Body webhook notifikasi Midtrans. gross_amount sengaja string mentah karena dipakai apa adanya untuk signature
public class MidtransNotificationDto
{
    [JsonPropertyName("order_id")]
    public string? order_id { get; set; }

    [JsonPropertyName("status_code")]
    public string? status_code { get; set; }

    [JsonPropertyName("gross_amount")]
    public string? gross_amount { get; set; }

    [JsonPropertyName("signature_key")]
    public string? signature_key { get; set; }

    [JsonPropertyName("transaction_status")]
    public string? transaction_status { get; set; }

    [JsonPropertyName("fraud_status")]
    public string? fraud_status { get; set; }
}
