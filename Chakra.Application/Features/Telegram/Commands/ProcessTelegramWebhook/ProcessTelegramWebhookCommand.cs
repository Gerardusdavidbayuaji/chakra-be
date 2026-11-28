using MediatR;
using Chakra.Application.Common;
using Chakra.Application.Features.Telegram.Dtos;

namespace Chakra.Application.Features.Telegram.Commands.ProcessTelegramWebhook;

public record ProcessTelegramWebhookCommand(string? SecretToken, TelegramUpdateDto Update) : IRequest<Result<string>>;
