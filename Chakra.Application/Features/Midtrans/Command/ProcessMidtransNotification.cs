using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Midtrans.Dtos;
using Chakra.Application.Features.Midtrans.Settings;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Midtrans.Command;

public record ProcessMidtransNotificationCommand(MidtransNotificationDto Notification) : IRequest<Result<string>>;

public class ProcessMidtransNotificationCommandHandler
    : IRequestHandler<ProcessMidtransNotificationCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMidtransClient _midtransClient;
    private readonly MidtransSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessMidtransNotificationCommandHandler> _logger;

    public ProcessMidtransNotificationCommandHandler(
        IApplicationDbContext context,
        IMidtransClient midtransClient,
        IOptions<MidtransSettings> settings,
        TimeProvider timeProvider,
        ILogger<ProcessMidtransNotificationCommandHandler> logger)
    {
        _context = context;
        _midtransClient = midtransClient;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(ProcessMidtransNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = request.Notification;

        if (!IsValidSignature(notification))
        {
            _logger.LogWarning("Invalid Midtrans signature for order {OrderId}. Check Midtrans:SignatureKey.",
                notification.order_id);
            return Result<string>.Unauthorized("Invalid signature.");
        }

        var orderId = notification.order_id!;

        MidtransStatus? status;
        try
        {
            status = await _midtransClient.GetStatusAsync(orderId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not confirm Midtrans status for order {OrderId}.", orderId);
            return Result<string>.Unavailable("Payment status could not be confirmed.");
        }

        if (status == null)
        {
            _logger.LogWarning("Midtrans has no status for order {OrderId}. Ignored.", orderId);
            return Result<string>.Success("Ignored.");
        }

        if (string.IsNullOrEmpty(status.TransactionStatus))
        {
            _logger.LogWarning("Midtrans returned no transaction status for order {OrderId}.", orderId);
            return Result<string>.Unavailable("Payment status could not be confirmed.");
        }

        var transaction = await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.OrderId == orderId, cancellationToken);

        if (transaction == null)
        {
            _logger.LogWarning("Payment transaction for order {OrderId} not found. Ignored.", orderId);
            return Result<string>.Success("Ignored.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (IsSettled(status))
        {
            if (transaction.IsFinal)
                return Result<string>.Success("Already processed.");

            await ApplySettlementAsync(transaction, status, now, cancellationToken);
        }
        else
        {
            ApplyNonSettlement(transaction, status, now);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<string>.Unavailable("Payment is being modified, please retry.");
        }

        return Result<string>.Success("Processed.");
    }

    private async Task ApplySettlementAsync(PaymentTransaction transaction, MidtransStatus status,
        DateTime now, CancellationToken cancellationToken)
    {
        var settledAt = status.SettlementTimeUtc ?? now;

        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == transaction.PremiId, cancellationToken);

        if (premi == null)
        {
            transaction.MarkUnapplied(PaymentRejectionReason.PremiNotActive, settledAt,
                status.PaymentType, status.TransactionId);
            return;
        }

        // Parse invariant: "1714285.00" harus terbaca 1714285 di mesin ber-locale id-ID
        if (!decimal.TryParse(status.GrossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var gross)
            || gross != transaction.Amount)
        {
            transaction.MarkUnapplied(PaymentRejectionReason.AmountMismatch, settledAt,
                status.PaymentType, status.TransactionId);
            return;
        }

        var rejection = premi.CheckCanPay(transaction.InstallmentId);
        if (rejection != null)
        {
            transaction.MarkUnapplied(rejection.Value, settledAt, status.PaymentType, status.TransactionId);
            return;
        }

        premi.MarkInstallmentPaid(transaction.InstallmentId, settledAt, transaction.OrderId);
        transaction.MarkPaid(settledAt, status.PaymentType, status.TransactionId);
    }

    private void ApplyNonSettlement(PaymentTransaction transaction, MidtransStatus status, DateTime now)
    {
        switch (status.TransactionStatus)
        {
            case "deny":
            case "cancel":
            case "failure":
                if (transaction.Status == PaymentTransactionStatus.Pending)
                    transaction.MarkFailed();
                break;

            case "expire":
                if (transaction.Status == PaymentTransactionStatus.Pending)
                    transaction.MarkExpired();
                break;

            case "refund":
                if (transaction.Status == PaymentTransactionStatus.Unapplied)
                    transaction.MarkRefunded("midtrans", null, now, null);
                else if (transaction.Status != PaymentTransactionStatus.Refunded)
                    _logger.LogWarning("Refund received for order {OrderId} with status {Status}; manual handling required.",
                        transaction.OrderId, transaction.Status);
                break;

            case "partial_refund":
                _logger.LogWarning("Partial refund received for order {OrderId}; manual handling required.",
                    transaction.OrderId);
                break;

            case "pending":
            case "authorize":
            case "capture": // capture dengan fraud_status bukan accept (mis. challenge): menunggu keputusan akhir
                break;

            default:
                _logger.LogInformation("Unhandled Midtrans status {Status} for order {OrderId}.",
                    status.TransactionStatus, transaction.OrderId);
                break;
        }
    }

    private static bool IsSettled(MidtransStatus status) =>
        status.TransactionStatus == "settlement"
        || (status.TransactionStatus == "capture" && status.FraudStatus == "accept");

    private bool IsValidSignature(MidtransNotificationDto n) =>
        MidtransSignature.IsValid(n.order_id, n.status_code, n.gross_amount, n.signature_key,
            _settings.NotificationSignatureKey);
}
