using FluentValidation;

namespace Chakra.Application.Features.Payments.Commands.RefundPayment;

public sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
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
