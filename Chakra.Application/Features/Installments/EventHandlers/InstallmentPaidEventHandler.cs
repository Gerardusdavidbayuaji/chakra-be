using MediatR;
using Microsoft.EntityFrameworkCore;

using Chakra.Application.Common;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Events;

namespace Chakra.Application.Features.Installments.EventHandlers;

// Dipanggil interceptor di dalam SaveChanges, jadi tidak boleh memanggil SaveChangesAsync
// dan tidak boleh memanggil API eksternal. Pesan masuk outbox dan ikut ter-commit bersama pembayaran.
public class InstallmentPaidEventHandler : INotificationHandler<InstallmentPaidEvent>
{
    private readonly IApplicationDbContext _context;

    public InstallmentPaidEventHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(InstallmentPaidEvent notification, CancellationToken cancellationToken)
    {
        var text = $"Pembayaran cicilan ke-{notification.InstallmentNumber} sebesar Rp{RupiahFormat.Format(notification.Amount)} berhasil.";

        if (notification.PremiCompleted)
            text += "\nSeluruh cicilan premi Anda sudah lunas.";

        _context.OutboxMessages.Add(OutboxMessage.CreateTelegram(OutboxMessageType.PaymentSuccess, notification.UserId, text));

        // Link lain untuk cicilan ini tidak boleh dibayar lagi. Hanya ditandai di sini,
        // pemanggilan Midtrans dilakukan PaymentExpiryProcessor di luar SaveChanges.
        var pending = await _context.PaymentTransactions
            .Where(t => t.InstallmentId == notification.InstallmentId && t.Status == PaymentTransactionStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var transaction in pending)
        {
            if (transaction.Status != PaymentTransactionStatus.Pending
                || transaction.ExpireRequested
                || transaction.OrderId == notification.MidtransOrderId)
                continue;

            transaction.RequestExpire();
        }
    }
}
