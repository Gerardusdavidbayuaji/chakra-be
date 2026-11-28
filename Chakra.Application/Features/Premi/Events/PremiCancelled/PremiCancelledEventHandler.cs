using MediatR;
using Microsoft.EntityFrameworkCore;
using Chakra.Application.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Events;

namespace Chakra.Application.Features.Premi.Events.PremiCancelled;

// Dipanggil interceptor di dalam SaveChanges, jadi tidak boleh memanggil SaveChangesAsync
// dan tidak boleh memanggil API eksternal. Expire ke Midtrans dilakukan PaymentExpiryProcessor.
internal sealed class PremiCancelledEventHandler : INotificationHandler<PremiCancelledEvent>
{
    private readonly IApplicationDbContext _context;

    public PremiCancelledEventHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(PremiCancelledEvent notification, CancellationToken cancellationToken)
    {
        var pending = await _context.PaymentTransactions
            .Where(t => t.PremiId == notification.PremiId && t.Status == PaymentTransactionStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var transaction in pending)
        {
            if (transaction.Status != PaymentTransactionStatus.Pending || transaction.ExpireRequested)
                continue;

            transaction.RequestExpire();
        }
    }
}
