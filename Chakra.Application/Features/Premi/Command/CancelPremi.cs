using Microsoft.EntityFrameworkCore;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Domain.Entities.Enums;
using Chakra.Application.Common;
using Chakra.Application.Mappers;

namespace Chakra.Application.Features.Premi.Command;

public record CancelPremiRequest(Guid Id) : IAuthorizedRequest<Result<PremiResponseDto>>;

public class CancelPremiRequestHandler : IRequestHandler<CancelPremiRequest, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public CancelPremiRequestHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PremiResponseDto>> Handle(CancelPremiRequest request, CancellationToken cancellationToken)
    {
        var premi = await _context.Premis
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.Failure("Premi not found.");

        if (premi.Status == PremiStatus.Cancelled)
            return Result<PremiResponseDto>.Failure("Premi status is now cancelled.");

        premi.Status = PremiStatus.Cancelled;
        premi.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}