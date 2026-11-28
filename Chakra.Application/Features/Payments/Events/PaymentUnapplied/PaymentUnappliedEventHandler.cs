using MediatR;
using Microsoft.EntityFrameworkCore;
using Chakra.Application.Common;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Events;

namespace Chakra.Application.Features.Payments.Events.PaymentUnapplied;

// Dipanggil interceptor di dalam SaveChanges, jadi tidak boleh memanggil SaveChangesAsync
// dan tidak boleh memanggil API eksternal. Alert masuk outbox dan ikut ter-commit bersama transaksi.
internal sealed class PaymentUnappliedEventHandler : INotificationHandler<PaymentUnappliedEvent>
{
    private readonly IApplicationDbContext _context;

    public PaymentUnappliedEventHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(PaymentUnappliedEvent notification, CancellationToken cancellationToken)
    {
        var adminIds = await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Admin && u.TelegramChatId != null)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count == 0)
            return;

        var text = "Dana pembayaran tidak dapat diterapkan.\n"
                   + $"Order: {notification.OrderId}\n"
                   + $"Nominal: Rp{RupiahFormat.Format(notification.Amount)}\n"
                   + $"Alasan: {notification.Reason}\n"
                   + "Silakan alokasikan atau refund lewat menu pembayaran.";

        foreach (var adminId in adminIds)
            _context.OutboxMessages.Add(OutboxMessage.CreateTelegram(OutboxMessageType.AdminAlert, adminId, text));
    }
}
