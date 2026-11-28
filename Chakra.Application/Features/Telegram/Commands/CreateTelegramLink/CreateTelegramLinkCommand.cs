using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Dtos;

namespace Chakra.Application.Features.Telegram.Commands.CreateTelegramLink;

public record CreateTelegramLinkCommand : IAuthorizedRequest<Result<TelegramLinkResponseDto>>;
