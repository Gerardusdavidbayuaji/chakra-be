using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Chakra.Application.Common;
using Chakra.Application.Features.Midtrans.Dtos;
using Chakra.Application.Features.Midtrans.Settings;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Services;

public class PaymentLinkService : IPaymentLinkService
{
    private static readonly TimeSpan ReuseThreshold = TimeSpan.FromMinutes(5);

    private readonly IApplicationDbContext _context;
    private readonly IMidtransClient _midtransClient;
    private readonly MidtransSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentLinkService> _logger;

    public PaymentLinkService(
        IApplicationDbContext context,
        IMidtransClient midtransClient,
        IOptions<MidtransSettings> settings,
        TimeProvider timeProvider,
        ILogger<PaymentLinkService> logger)
    {
        _context = context;
        _midtransClient = midtransClient;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<PaymentLinkDto>> GetOrCreateAsync(
        InstallmentId installmentId, CancellationToken cancellationToken)
    {
        // 1. Premi + cicilan + user
        var premi = await _context.Premis
            .Include(p => p.Installments)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Installments.Any(i => i.Id == installmentId), cancellationToken);

        if (premi == null)
            return Result<PaymentLinkDto>.NotFound("Installment not found.");

        // 2. Cicilan harus boleh dibayar
        var rejection = premi.CheckCanPay(installmentId);
        if (rejection != null)
            return Result<PaymentLinkDto>.Failure(RejectionMessage(rejection.Value));

        var installment = premi.Installments.First(i => i.Id == installmentId);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // 3. Pakai ulang transaksi Pending yang masih layak
        var pending = await _context.PaymentTransactions
            .Where(t => t.InstallmentId == installmentId && t.Status == PaymentTransactionStatus.Pending)
            .ToListAsync(cancellationToken);

        var reusable = pending.FirstOrDefault(t =>
            !t.ExpireRequested && t.RedirectUrl != null && t.ExpiresAt > now.Add(ReuseThreshold));

        if (reusable != null)
            return Result<PaymentLinkDto>.Success(ToDto(reusable));

        // 4. Sisanya (hampir expire / tanpa token) ditutup agar unique index Pending bebas
        //    Disimpan terpisah: EF tidak menjamin UPDATE sebelum INSERT dalam satu SaveChanges
        PaymentTransaction transaction;
        PaymentTransaction? newTransaction = null;
        try
        {
            if (pending.Count > 0)
            {
                foreach (var stale in pending)
                    stale.MarkExpired();

                await _context.SaveChangesAsync(cancellationToken);
            }

            // 5. Simpan dulu sebelum memanggil Snap
            newTransaction = PaymentTransaction.Create(installment, now, _settings.PaymentExpiryHours);
            _context.PaymentTransactions.Add(newTransaction);
            transaction = newTransaction;

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Buang insert yang gagal agar SaveChanges berikutnya di scope yang sama tidak mengulanginya
            if (newTransaction != null)
                _context.Entry(newTransaction).State = EntityState.Detached;

            return Result<PaymentLinkDto>.Unavailable("Payment link is being created, please retry.");
        }

        // 6. Panggil Snap (transaction pasti terisi di sini)
        SnapResult snap;
        try
        {
            snap = await _midtransClient.CreateSnapTransactionAsync(new SnapRequest(
                transaction.OrderId,
                (long)transaction.Amount,
                premi.User.Name,
                premi.User.Email,
                $"Cicilan ke-{installment.InstallmentNumber}",
                _settings.PaymentExpiryHours), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // 7. Gagal
            _logger.LogError(ex, "Failed to create Snap transaction for order {OrderId}", transaction.OrderId);
            transaction.MarkFailed();
            await _context.SaveChangesAsync(cancellationToken);
            return Result<PaymentLinkDto>.Unavailable("Payment gateway is unavailable, please try again.");
        }

        // 7. Sukses
        transaction.AttachSnap(snap.Token, snap.RedirectUrl);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<PaymentLinkDto>.Success(ToDto(transaction));
    }

    private static PaymentLinkDto ToDto(PaymentTransaction t) => new()
    {
        OrderId = t.OrderId,
        RedirectUrl = t.RedirectUrl!,
        SnapToken = t.SnapToken!,
        Amount = t.Amount,
        ExpiresAt = t.ExpiresAt
    };

    // Pesan sama dengan Premi.MarkInstallmentPaid
    private static string RejectionMessage(PaymentRejectionReason reason) => reason switch
    {
        PaymentRejectionReason.PremiNotActive => "Premi is not active.",
        PaymentRejectionReason.InstallmentNotFound => "Installment not found in this premi.",
        PaymentRejectionReason.InstallmentAlreadyPaid => "Installment is already paid.",
        PaymentRejectionReason.PreviousInstallmentUnpaid => "Previous installments must be paid first.",
        _ => "Installment cannot be paid."
    };
}
