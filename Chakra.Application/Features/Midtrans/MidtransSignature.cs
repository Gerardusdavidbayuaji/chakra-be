using System.Security.Cryptography;
using System.Text;

namespace Chakra.Application.Features.Midtrans;

public static class MidtransSignature
{
    // signature_key = SHA512(order_id + status_code + gross_amount + key), hex huruf kecil
    public static bool IsValid(string? orderId, string? statusCode, string? grossAmount,
        string? signatureKey, string? key)
    {
        if (string.IsNullOrEmpty(key)
            || string.IsNullOrEmpty(orderId)
            || string.IsNullOrEmpty(statusCode)
            || string.IsNullOrEmpty(grossAmount)
            || string.IsNullOrEmpty(signatureKey))
            return false;

        var hash = SHA512.HashData(Encoding.UTF8.GetBytes(orderId + statusCode + grossAmount + key));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureKey.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expected));
    }
}
