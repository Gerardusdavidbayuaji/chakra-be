using MediatR;
using Chakra.Application.Common;
using Chakra.Application.Features.Midtrans.Dtos;

namespace Chakra.Application.Features.Midtrans.Commands.ProcessMidtransNotification;

public record ProcessMidtransNotificationCommand(MidtransNotificationDto Notification) : IRequest<Result<string>>;
