using System.Security.Cryptography;
using System.Text;
using Chakra.Application.Features.Midtrans;

namespace Chakra.UnitTests.MidtransSignatureChecks;

public class MidtransSignatureTests
{
    private const string OrderId = "CHK-1";
    private const string StatusCode = "200";
    private const string Gross = "1714285.00";
    private const string Key = "KEY";

    private static string Sign(string orderId, string statusCode, string gross, string key) =>
        Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(orderId + statusCode + gross + key)))
            .ToLowerInvariant();

    [Fact]
    public void ValidSignature_ReturnsTrue()
    {
        var signature = Sign(OrderId, StatusCode, Gross, Key);

        Assert.True(MidtransSignature.IsValid(OrderId, StatusCode, Gross, signature, Key));
    }

    [Fact]
    public void WrongKey_ReturnsFalse()
    {
        var signature = Sign(OrderId, StatusCode, Gross, Key);

        Assert.False(MidtransSignature.IsValid(OrderId, StatusCode, Gross, signature, "OTHER"));
    }

    [Fact]
    public void ReformattedGrossAmount_ReturnsFalse()
    {
        var signature = Sign(OrderId, StatusCode, Gross, Key);

        Assert.False(MidtransSignature.IsValid(OrderId, StatusCode, "1714285", signature, Key));
    }

    [Fact]
    public void EmptyKey_ReturnsFalse()
    {
        var signature = Sign(OrderId, StatusCode, Gross, "");

        Assert.False(MidtransSignature.IsValid(OrderId, StatusCode, Gross, signature, ""));
        Assert.False(MidtransSignature.IsValid(OrderId, StatusCode, Gross, signature, null));
    }

    [Theory]
    [InlineData(null, "200", "1714285.00")]
    [InlineData("CHK-1", null, "1714285.00")]
    [InlineData("CHK-1", "200", null)]
    [InlineData("", "200", "1714285.00")]
    public void MissingInput_ReturnsFalse(string? orderId, string? statusCode, string? gross)
    {
        var signature = Sign(OrderId, StatusCode, Gross, Key);

        Assert.False(MidtransSignature.IsValid(orderId, statusCode, gross, signature, Key));
    }

    [Fact]
    public void MissingSignature_ReturnsFalse()
    {
        Assert.False(MidtransSignature.IsValid(OrderId, StatusCode, Gross, null, Key));
        Assert.False(MidtransSignature.IsValid(OrderId, StatusCode, Gross, "", Key));
    }

    // Perilaku saat ini (didokumentasikan): signature dari client dibandingkan secara case-insensitive,
    // jadi hex huruf besar tetap dianggap valid.
    [Fact]
    public void UppercaseHexSignature_IsAcceptedToday()
    {
        var signature = Sign(OrderId, StatusCode, Gross, Key).ToUpperInvariant();

        Assert.True(MidtransSignature.IsValid(OrderId, StatusCode, Gross, signature, Key));
    }
}
