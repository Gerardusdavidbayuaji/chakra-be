using Microsoft.EntityFrameworkCore;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Premi.Command;

public record CancelPremiRequest(Guid Id) : IAdminRequest<Result<PremiResponseDto>>;

public class CancelPremiRequestHandler : IRequestHandler<CancelPremiRequest, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public CancelPremiRequestHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PremiResponseDto>> Handle(CancelPremiRequest request, CancellationToken cancellationToken)
    {
        var premiId = new PremiId(request.Id);

        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.NotFound("Premi not found.");

        try
        {
            premi.Cancel();
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

        return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}
