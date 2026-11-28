using Microsoft.EntityFrameworkCore;
using MediatR;
using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Payments.Commands.RefundPayment;

internal sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, Result<PaymentTransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public RefundPaymentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PaymentTransactionDto>> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.DatabaseUserId;
        if (adminId == null)
            return Result<PaymentTransactionDto>.Unauthorized("Unauthorized");

        var paymentId = new PaymentTransactionId(request.PaymentId);
        var transaction = await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == paymentId, cancellationToken);

        if (transaction == null)
            return Result<PaymentTransactionDto>.NotFound("Payment not found.");

        try
        {
            transaction.MarkRefunded(request.Reference.Trim(), adminId.Value,
                _timeProvider.GetUtcNow().UtcDateTime, request.Note);
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
