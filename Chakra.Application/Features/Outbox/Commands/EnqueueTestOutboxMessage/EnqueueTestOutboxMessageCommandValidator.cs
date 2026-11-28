using FluentValidation;

namespace Chakra.Application.Features.Outbox.Commands.EnqueueTestOutboxMessage;

public sealed class EnqueueTestOutboxMessageCommandValidator : AbstractValidator<EnqueueTestOutboxMessageCommand>
{
    public EnqueueTestOutboxMessageCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required")
            .MaximumLength(4096).WithMessage("Text must be at most 4000 characters");
    }
}
