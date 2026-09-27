using Microsoft.EntityFrameworkCore;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Domain.Entities.Enums;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities;

namespace Chakra.Application.Features.Premi.Command;

public class UpdatePremiInput : IAuthorizedRequest<Result<PremiResponseDto>> 
{
        public Guid Id { get; set; }
        public decimal TotalAmount { get; set; }
        public int Tenor { get; set; }
        public int DueDay { get; set; }
        public int GracePeriodDays { get; set; }
        public DateOnly StartDate { get; set; }
}

public class UpdatePremiRequestHandler : IRequestHandler<UpdatePremiInput, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdatePremiRequestHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PremiResponseDto>> Handle(UpdatePremiInput request, CancellationToken cancellationToken)
    {
        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.Failure("Premi not found.");

        if (premi.Status == PremiStatus.Cancelled)
            return Result<PremiResponseDto>.Failure("Premi that have been cancelled cannot be changed.");

        var hasPaidInstallment = premi.Installments.Any(i => i.Status == InstallmentStatus.Paid);
        if (hasPaidInstallment)
            return Result<PremiResponseDto>.Failure("Premi cannot be changed because installments have already been paid.");

        var installmentAmount = Math.Round(request.TotalAmount / request.Tenor, 2);

        premi.TotalAmount = request.TotalAmount;
        premi.InstallmentAmount = installmentAmount;
        premi.Tenor = request.Tenor;
        premi.DueDay = request.DueDay;
        premi.GracePeriodDays = request.GracePeriodDays;
        premi.StartDate = request.StartDate;
        premi.UpdatedAt = DateTime.UtcNow;

        _context.Installments.RemoveRange(premi.Installments);
        premi.Installments.Clear();

        for (int i = 1; i <= request.Tenor; i++)
        {
            var dueDate = request.StartDate.AddMonths(i);
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
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        
        return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}
    
    