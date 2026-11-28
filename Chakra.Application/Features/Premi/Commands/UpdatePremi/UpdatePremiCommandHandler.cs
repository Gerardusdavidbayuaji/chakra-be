using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;
using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Premi.Commands.UpdatePremi;

internal sealed class UpdatePremiCommandHandler : IRequestHandler<UpdatePremiCommand, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdatePremiCommandHandler> _logger;

    public UpdatePremiCommandHandler(IApplicationDbContext context, ILogger<UpdatePremiCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<PremiResponseDto>> Handle(UpdatePremiCommand request, CancellationToken cancellationToken)
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
