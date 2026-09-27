using Microsoft.EntityFrameworkCore;
using FluentValidation;
using MediatR;

using PremiEntity = Chakra.Domain.Entities.Premi;
using Chakra.Application.Features.Premi.Dtos;
using Chakra.Domain.Entities.Enums;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities;

namespace Chakra.Application.Features.Premi.Command;

public class CreatePremiInput : IAuthorizedRequest<Result<PremiResponseDto>>
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
                .GreaterThan(0).WithMessage("Total  amount must be greater than zero");

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
            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
            if (!userExists) 
                return Result<PremiResponseDto>.Failure("User not found");

            var installmentAmount = Math.Round(request.TotalAmount / request.Tenor, 2);

            var premi = new PremiEntity
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                TotalAmount = request.TotalAmount,
                InstallmentAmount = installmentAmount,
                Tenor = request.Tenor,
                DueDay = request.DueDay,
                GracePeriodDays = request.GracePeriodDays,
                StartDate = request.StartDate,
                Status = PremiStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            

            //generate installments
            for (int i = 1; i <= request.Tenor; i++)
            {
                var dueDate = request.StartDate.AddMonths(i);
                // Sesuaikan hari jatuh tempo
                var maxDay = DateTime.DaysInMonth(dueDate.Year, dueDate.Month);
                var actualDueDay = Math.Min(request.DueDay, maxDay);
                dueDate = new DateOnly(dueDate.Year, dueDate.Month, actualDueDay);
                
                premi.Installments.Add(new Installment
                {
                    Id = Guid.NewGuid(),
                    PremiId = premi.Id,
                    InstallmentNumber = i,
                    DueDate = dueDate,
                    Amount = installmentAmount,
                    Status = InstallmentStatus.Pending,
                    ReminderCount = 0,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            
            _context.Premis.Add(premi);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}