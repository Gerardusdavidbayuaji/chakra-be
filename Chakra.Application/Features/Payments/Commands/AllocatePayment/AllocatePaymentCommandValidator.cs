using FluentValidation;

namespace Chakra.Application.Features.Payments.Commands.AllocatePayment;

public sealed class AllocatePaymentCommandValidator : AbstractValidator<AllocatePaymentCommand>
{
    public AllocatePaymentCommandValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(400).WithMessage("Note must not exceed 400 characters");
    }
}
