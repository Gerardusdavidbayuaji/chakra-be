using Microsoft.EntityFrameworkCore;
using MediatR;
using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Payments.Commands.AllocatePayment;

internal sealed class AllocatePaymentCommandHandler: IRequestHandler<AllocatePaymentCommand, Result<PaymentTransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AllocatePaymentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PaymentTransactionDto>> Handle(AllocatePaymentCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.DatabaseUserId;
        if (adminId == null)
            return Result<PaymentTransactionDto>.Unauthorized("Unauthorized");

        var paymentId = new PaymentTransactionId(request.PaymentId);
        var transaction = await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == paymentId, cancellationToken);

        if (transaction == null)
            return Result<PaymentTransactionDto>.NotFound("Payment not found.");

        if (transaction.Status != PaymentTransactionStatus.Unapplied)
            return Result<PaymentTransactionDto>.Failure("Only unapplied payments can be allocated.");

        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == transaction.PremiId, cancellationToken);

        if (premi == null)
            return Result<PaymentTransactionDto>.NotFound("Premi not found.");

        if (premi.Status != PremiStatus.Active)
            return Result<PaymentTransactionDto>.Failure("Premi is not active, this payment can only be refunded.");

        var target = premi.NextUnpaidInstallment();
        if (target == null)
            return Result<PaymentTransactionDto>.Failure("Premi has no unpaid installment, this payment can only be refunded.");

        if (transaction.Amount != target.Amount)
            return Result<PaymentTransactionDto>.Failure(
                "Payment amount does not match the next installment, this payment can only be refunded.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            // PaidAt = waktu uang diterima, bukan waktu admin mengalokasikan
            premi.MarkInstallmentPaid(target.Id, transaction.SettledAt ?? now, transaction.OrderId);
            transaction.MarkAllocated(target.Id, adminId.Value, now, request.Note);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<PaymentTransactionDto>.Success(transaction.ToDto());
        }
        catch (DomainException ex)
        {
            return Result<PaymentTransactionDto>.Failure(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PaymentTransactionDto>.Failure("Payment was modified by another request, please retry.");
        }
    }
}
