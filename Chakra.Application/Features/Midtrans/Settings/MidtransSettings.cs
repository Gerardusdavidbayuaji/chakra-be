namespace Chakra.Application.Features.Midtrans.Settings;

public class MidtransSettings
{
    public const string SectionName = "Midtrans";

    public string MerchantId { get; set; } = string.Empty;
    public string ClientKey { get; set; } = string.Empty;
    public string ServerKey { get; set; } = string.Empty; // autentikasi ke Snap / Core API
    public string SignatureKey { get; set; } = string.Empty; // "Signature Secret Key" di dashboard baru; kosong = pakai ServerKey
    public bool IsProduction { get; set; } = false;
    public int PaymentExpiryHours { get; set; } = 24;
    public int ExpiryProcessorIntervalSeconds { get; set; } = 60;

    // Kunci untuk memverifikasi signature_key notifikasi
    public string NotificationSignatureKey =>
        string.IsNullOrEmpty(SignatureKey) ? ServerKey : SignatureKey;

    public string SnapBaseUrl => IsProduction
        ? "https://app.midtrans.com/"
        : "https://app.sandbox.midtrans.com/";

    public string ApiBaseUrl => IsProduction
        ? "https://api.midtrans.com/"
        : "https://api.sandbox.midtrans.com/";
}
