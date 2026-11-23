using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chakra.Application.Common;
using Chakra.Application.Features.Midtrans.Dtos;
using Chakra.Application.Features.Midtrans.Settings;
using Microsoft.Extensions.Options;

namespace Chakra.Infrastructure.Services.Midtrans;

public class MidtransClient : IMidtransClient
{
    private const int MaxItemNameLength = 50;
    private static readonly TimeSpan WibOffset = TimeSpan.FromHours(7);

    private readonly HttpClient _httpClient;
    private readonly MidtransSettings _settings;

    public MidtransClient(HttpClient httpClient, IOptions<MidtransSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<SnapResult> CreateSnapTransactionAsync(SnapRequest request, CancellationToken cancellationToken = default)
    {
        var itemName = request.ItemName.Length > MaxItemNameLength
            ? request.ItemName[..MaxItemNameLength]
            : request.ItemName;

        var payload = new SnapTransactionBody(
            new TransactionDetailsBody(request.OrderId, request.GrossAmount),
            new CustomerDetailsBody(request.CustomerName, request.CustomerEmail),
            [new ItemDetailBody(request.OrderId, request.GrossAmount, 1, itemName)],
            new ExpiryBody("hour", request.ExpiryHours));

        using var message = BuildRequest(HttpMethod.Post, _settings.SnapBaseUrl + "snap/v1/transactions");
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode != HttpStatusCode.Created)
            throw Failure(response.StatusCode, body);

        var result = Deserialize<SnapResponse>(body);
        if (result is null || string.IsNullOrEmpty(result.Token) || string.IsNullOrEmpty(result.RedirectUrl))
            throw Failure(response.StatusCode, body);

        return new SnapResult(result.Token, result.RedirectUrl);
    }

    public async Task<MidtransStatus?> GetStatusAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var message = BuildRequest(HttpMethod.Get,
            _settings.ApiBaseUrl + $"v2/{Uri.EscapeDataString(orderId)}/status");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw Failure(response.StatusCode, body);

        var status = Deserialize<StatusResponse>(body);
        if (status is null)
            throw Failure(response.StatusCode, body);

        // Midtrans membalas 404 di dalam body dengan HTTP 200 untuk order yang belum ada
        if (status.StatusCode == "404")
            return null;

        return new MidtransStatus(
            status.OrderId ?? orderId,
            status.StatusCode ?? string.Empty,
            status.TransactionStatus ?? string.Empty,
            status.FraudStatus,
            status.GrossAmount ?? string.Empty,
            status.PaymentType,
            status.TransactionId,
            ParseSettlementTime(status.SettlementTime));
    }

    public async Task ExpireAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var message = BuildRequest(HttpMethod.Post,
            _settings.ApiBaseUrl + $"v2/{Uri.EscapeDataString(orderId)}/expire");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        if (response.IsSuccessStatusCode)
        {
            var result = Deserialize<StatusResponse>(body);
            if (result?.StatusCode is "200" or "407" or "404")
                return;
        }

        throw Failure(response.StatusCode, body);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var message = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.ServerKey + ":"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return message;
    }

    // Pesan sengaja tidak memuat URL/header request supaya server key tidak bocor
    private static InvalidOperationException Failure(HttpStatusCode status, string body) =>
        new($"Midtrans {(int)status}: {body}");

    private static T? Deserialize<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // settlement_time berformat "yyyy-MM-dd HH:mm:ss" dalam WIB (UTC+7)
    private static DateTime? ParseSettlementTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var wib))
            return null;

        return DateTime.SpecifyKind(wib - WibOffset, DateTimeKind.Utc);
    }

    // Midtrans kadang mengirim angka sebagai string ("10000.00"), kadang sebagai number
    private sealed class FlexibleStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => Encoding.UTF8.GetString(reader.HasValueSequence
                    ? reader.ValueSequence.ToArray()
                    : reader.ValueSpan),
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Unexpected token {reader.TokenType}")
            };

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private record SnapTransactionBody(
        [property: JsonPropertyName("transaction_details")] TransactionDetailsBody TransactionDetails,
        [property: JsonPropertyName("customer_details")] CustomerDetailsBody CustomerDetails,
        [property: JsonPropertyName("item_details")] ItemDetailBody[] ItemDetails,
        [property: JsonPropertyName("expiry")] ExpiryBody Expiry);

    private record TransactionDetailsBody(
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("gross_amount")] long GrossAmount);

    private record CustomerDetailsBody(
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("email")] string Email);

    private record ItemDetailBody(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("price")] long Price,
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("name")] string Name);

    private record ExpiryBody(
        [property: JsonPropertyName("unit")] string Unit,
        [property: JsonPropertyName("duration")] int Duration);

    private record SnapResponse(
        [property: JsonPropertyName("token")] string? Token,
        [property: JsonPropertyName("redirect_url")] string? RedirectUrl);

    private record StatusResponse(
        [property: JsonPropertyName("order_id")] string? OrderId,
        [property: JsonPropertyName("status_code")] string? StatusCode,
        [property: JsonPropertyName("transaction_status")] string? TransactionStatus,
        [property: JsonPropertyName("fraud_status")] string? FraudStatus,
        [property: JsonPropertyName("gross_amount"), JsonConverter(typeof(FlexibleStringConverter))] string? GrossAmount,
        [property: JsonPropertyName("payment_type")] string? PaymentType,
        [property: JsonPropertyName("transaction_id")] string? TransactionId,
        [property: JsonPropertyName("settlement_time")] string? SettlementTime);
}
