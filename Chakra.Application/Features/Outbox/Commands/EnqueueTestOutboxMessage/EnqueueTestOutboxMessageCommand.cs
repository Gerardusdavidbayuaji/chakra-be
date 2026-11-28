using MediatR;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Outbox.Commands.EnqueueTestOutboxMessage;

public record EnqueueTestOutboxMessageCommand(Guid UserId, string Text) : IRequest<Result<Guid>>;
