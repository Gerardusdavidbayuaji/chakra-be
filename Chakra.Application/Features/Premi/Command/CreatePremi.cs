using Microsoft.EntityFrameworkCore;
using FluentValidation;
using MediatR;

using PremiEntity = Chakra.Domain.Entities.Premi;
using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Premi.Command;

public class CreatePremiInput : IAdminRequest<Result<PremiResponseDto>>
{
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public int Tenor { get; set; }
    public int DueDay { get; set; }
    public int GracePeriodDays { get; set; }
    public DateOnly StartDate { get; set; }
}

public class CreatePremiValidator : AbstractValidator<CreatePremiInput>
{
    public CreatePremiValidator()
    {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("userId is required");

            RuleFor(x => x.TotalAmount)
                .GreaterThan(0).WithMessage("Total  amount must be greater than zero")
                .Must(x => x % 1 == 0).WithMessage("Total amount must be a whole rupiah amount");

            RuleFor(x => x)
                .Must(x => x.TotalAmount >= x.Tenor).WithMessage("Total amount must be at least equal to tenor")
                .OverridePropertyName(nameof(CreatePremiInput.TotalAmount));

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

public class CreatePremiRequestHandler : IRequestHandler<CreatePremiInput, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public CreatePremiRequestHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PremiResponseDto>> Handle(CreatePremiInput request, CancellationToken cancellationToken)
    {
            var userId = new UserId(request.UserId);

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userExists)
                return Result<PremiResponseDto>.NotFound("User not found");

            PremiEntity premi;
            try
            {
                premi = PremiEntity.Create(userId, request.TotalAmount, request.Tenor, request.DueDay,
                    request.GracePeriodDays, request.StartDate);
            }
            catch (DomainException ex)
            {
                return Result<PremiResponseDto>.Failure(ex.Message);
            }

            _context.Premis.Add(premi);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}
