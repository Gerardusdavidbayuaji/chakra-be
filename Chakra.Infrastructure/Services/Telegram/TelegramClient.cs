using System.Net.Http.Json;
using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Settings;
using Microsoft.Extensions.Options;

namespace Chakra.Infrastructure.Services.Telegram;

public class TelegramClient : ITelegramClient
{
    private readonly HttpClient _httpClient;
    private readonly TelegramSettings _settings;

    public TelegramClient(HttpClient httpClient, IOptions<TelegramSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"bot{_settings.BotToken}/sendMessage",
            new { chat_id = chatId, text },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Jangan sertakan URL request karena berisi bot token
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Telegram sendMessage failed with status {(int)response.StatusCode}: {body}");
        }
    }
}
