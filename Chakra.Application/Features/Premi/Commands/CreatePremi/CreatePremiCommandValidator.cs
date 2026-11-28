using FluentValidation;

namespace Chakra.Application.Features.Premi.Commands.CreatePremi;

public sealed class CreatePremiCommandValidator : AbstractValidator<CreatePremiCommand>
{
    public CreatePremiCommandValidator()
    {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("userId is required");

            RuleFor(x => x.TotalAmount)
                .GreaterThan(0).WithMessage("Total  amount must be greater than zero")
                .Must(x => x % 1 == 0).WithMessage("Total amount must be a whole rupiah amount");

            RuleFor(x => x)
                .Must(x => x.TotalAmount >= x.Tenor).WithMessage("Total amount must be at least equal to tenor")
                .OverridePropertyName(nameof(CreatePremiCommand.TotalAmount));

            RuleFor(x => x.Tenor)
                .GreaterThan(0).WithMessage("Tenor must be greater than zero");

            RuleFor(x => x.DueDay)
                .InclusiveBetween(1, 31).WithMessage("DueDay  must be between 1 and 31");

            RuleFor(x => x.GracePeriodDays)
                .GreaterThanOrEqualTo(0).WithMessage("GracePeriodDays must be greater than or equal to 0");

            RuleFor(x => x.StartDate)
                .NotEmpty().WithMessage("StartDate  is required");
    }
}
