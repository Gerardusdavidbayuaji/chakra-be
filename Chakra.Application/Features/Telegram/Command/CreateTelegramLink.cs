using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Dtos;
using Chakra.Application.Features.Telegram.Helper;
using Chakra.Application.Features.Telegram.Settings;

namespace Chakra.Application.Features.Telegram.Command;

public record CreateTelegramLinkCommand : IAuthorizedRequest<Result<TelegramLinkResponseDto>>;

public class CreateTelegramLinkCommandHandler : IRequestHandler<CreateTelegramLinkCommand, Result<TelegramLinkResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TelegramSettings _settings;
    private readonly TimeProvider _timeProvider;

    public CreateTelegramLinkCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IOptions<TelegramSettings> settings,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<Result<TelegramLinkResponseDto>> Handle(CreateTelegramLinkCommand request, CancellationToken cancellationToken)
    {
        // AuthorizationBehavior sudah memastikan DatabaseUserId tidak null
        var userId = _currentUser.DatabaseUserId!.Value;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return Result<TelegramLinkResponseDto>.Failure("User not found.");

        var token = TelegramLinkToken.Generate();
        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_settings.LinkTokenLifetimeMinutes);

        user.SetTelegramLinkToken(TelegramLinkToken.Hash(token), expiresAt);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TelegramLinkResponseDto>.Success(new TelegramLinkResponseDto
        {
            DeepLink = $"https://t.me/{_settings.BotUsername}?start={token}",
            ExpiresAt = expiresAt
        });
    }
}
