using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FluentValidation;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Premi.Command;

public class UpdatePremiInput : IAdminRequest<Result<PremiResponseDto>>
{
        public Guid Id { get; set; }
        public decimal TotalAmount { get; set; }
        public int Tenor { get; set; }
        public int DueDay { get; set; }
        public int GracePeriodDays { get; set; }
        public DateOnly StartDate { get; set; }
}

public class UpdatePremiValidator : AbstractValidator<UpdatePremiInput>
{
    public UpdatePremiValidator()
    {
        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("Total amount must be greater than zero")
            .Must(x => x % 1 == 0).WithMessage("Total amount must be a whole rupiah amount");

        RuleFor(x => x)
            .Must(x => x.TotalAmount >= x.Tenor).WithMessage("Total amount must be at least equal to tenor")
            .OverridePropertyName(nameof(UpdatePremiInput.TotalAmount));

        RuleFor(x => x.Tenor)
            .GreaterThan(0).WithMessage("Tenor must be greater than zero");

        RuleFor(x => x.DueDay)
            .InclusiveBetween(1, 31).WithMessage("DueDay must be between 1 and 31");

        RuleFor(x => x.GracePeriodDays)
            .GreaterThanOrEqualTo(0).WithMessage("GracePeriodDays must be greater than or equal to 0");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("StartDate is required");
    }
}

public class UpdatePremiRequestHandler : IRequestHandler<UpdatePremiInput, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdatePremiRequestHandler> _logger;

    public UpdatePremiRequestHandler(IApplicationDbContext context, ILogger<UpdatePremiRequestHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<PremiResponseDto>> Handle(UpdatePremiInput request, CancellationToken cancellationToken)
    {
        var premiId = new PremiId(request.Id);

        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.NotFound("Premi not found.");

        try
        {
            // Cicilan lama dikeluarkan dari koleksi dan dihapus EF sebagai orphan
            premi.Update(request.TotalAmount, request.Tenor, request.DueDay,
                request.GracePeriodDays, request.StartDate);
        }
        catch (DomainException ex)
        {
            return Result<PremiResponseDto>.Failure(ex.Message);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PremiResponseDto>.Failure("Premi was modified by another request, please retry.");
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            _logger.LogError(ex, "Failed to update premi {PremiId}", request.Id);
            return Result<PremiResponseDto>.Failure("Premi cannot be changed because payment links have been created.");
        }

        return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}
