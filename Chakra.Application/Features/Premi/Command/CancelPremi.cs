using Microsoft.EntityFrameworkCore;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Domain.Entities.Enums;
using Chakra.Application.Common;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;

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
        var premiId = new PremiId(request.Id);

        var premi = await _context.Premis
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.Failure("Premi not found.");

        if (premi.Status == PremiStatus.Cancelled)
            return Result<PremiResponseDto>.Failure("Premi status is now cancelled.");

        premi.Status = PremiStatus.Cancelled;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}