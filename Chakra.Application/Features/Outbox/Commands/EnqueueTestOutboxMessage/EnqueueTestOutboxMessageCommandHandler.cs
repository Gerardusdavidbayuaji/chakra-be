using Microsoft.EntityFrameworkCore;
using MediatR;
using Chakra.Application.Common;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Outbox.Commands.EnqueueTestOutboxMessage;

internal sealed class EnqueueTestOutboxMessageCommandHandler : IRequestHandler<EnqueueTestOutboxMessageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public EnqueueTestOutboxMessageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(EnqueueTestOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = new UserId(request.UserId);

        var userExists = await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
            return Result<Guid>.Failure("User not found.");

        var message = OutboxMessage.CreateTelegram(OutboxMessageType.Test, userId, request.Text);
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(message.Id.Value);
    }
}
