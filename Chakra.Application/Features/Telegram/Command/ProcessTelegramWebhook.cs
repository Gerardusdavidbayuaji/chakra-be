using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Dtos;
using Chakra.Application.Features.Telegram.Helper;
using Chakra.Application.Features.Telegram.Settings;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Telegram.Command;

public record ProcessTelegramWebhookCommand(string? SecretToken, TelegramUpdateDto Update) : IRequest<Result<string>>;

public class ProcessTelegramWebhookCommandHandler : IRequestHandler<ProcessTelegramWebhookCommand, Result<string>>
{
    private const string StartCommand = "/start ";
    private const string LinkedMessage =
        "Akun Chakra kamu berhasil terhubung dengan Telegram. Notifikasi cicilan akan dikirim ke sini.";

    private readonly IApplicationDbContext _context;
    private readonly TelegramSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessTelegramWebhookCommandHandler> _logger;

    public ProcessTelegramWebhookCommandHandler(
        IApplicationDbContext context,
        IOptions<TelegramSettings> settings,
        TimeProvider timeProvider,
        ILogger<ProcessTelegramWebhookCommandHandler> logger)
    {
        _context = context;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(ProcessTelegramWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!IsValidSecret(request.SecretToken))
            return Result<string>.Failure("Invalid secret token.");

        var text = request.Update.Message?.Text;
        var chatId = request.Update.Message?.Chat?.Id;

        if (chatId == null || text == null || !text.StartsWith(StartCommand, StringComparison.Ordinal))
            return Result<string>.Success("Ignored.");

        var token = text[StartCommand.Length..].Trim();
        if (token.Length == 0)
            return Result<string>.Success("Ignored.");

        var tokenHash = TelegramLinkToken.Hash(token);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramLinkTokenHash == tokenHash, cancellationToken);

        if (user == null || user.TelegramLinkTokenExpiresAt < now)
        {
            _logger.LogInformation("Telegram link token not found or expired (update {UpdateId}).", request.Update.UpdateId);
            return Result<string>.Success("Ignored.");
        }

        user.LinkTelegram(chatId.Value);
        _context.OutboxMessages.Add(OutboxMessage.CreateTelegram(OutboxMessageType.TelegramLinked, user.Id, LinkedMessage));

        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Linked.");
    }

    private bool IsValidSecret(string? secretToken)
    {
        if (string.IsNullOrEmpty(_settings.WebhookSecret) || string.IsNullOrEmpty(secretToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(secretToken),
            Encoding.UTF8.GetBytes(_settings.WebhookSecret));
    }
}
