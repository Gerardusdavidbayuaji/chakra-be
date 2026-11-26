using Microsoft.EntityFrameworkCore;
using FluentValidation;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Payments.Command;

// Admin mencatat bahwa dana yang ditampung sudah dikembalikan manual (di luar sistem)
public record RefundPaymentCommand(Guid PaymentId, string Reference, string? Note) : IAdminRequest<Result<PaymentTransactionDto>>;

public class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("Reference is required")
            .MaximumLength(400).WithMessage("Reference must not exceed 400 characters");

        RuleFor(x => x.Note)
            .MaximumLength(400).WithMessage("Note must not exceed 400 characters");
    }
}

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, Result<PaymentTransactionDto>>
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
