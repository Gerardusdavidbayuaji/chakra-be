using Microsoft.EntityFrameworkCore;
using MediatR;
using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;

namespace Chakra.Application.Features.Premi.Queries.GetPremiById;

internal sealed class GetPremiByIdQueryHandler : IRequestHandler<GetPremiByIdQuery, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPremiByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PremiResponseDto>> Handle(GetPremiByIdQuery request, CancellationToken cancellationToken)
    {
        var premiId = new PremiId(request.Id);

        var premi = await _context.Premis
            .AsNoTracking()
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);

        // Premi milik orang lain dijawab sama dengan tidak ada, agar keberadaannya tidak bocor
        if (premi == null || (!_currentUser.IsAdmin && premi.UserId != _currentUser.DatabaseUserId))
            return Result<PremiResponseDto>.NotFound("Premi not found.");

        // InstallmentsPaid, RemainingAmount, NextDueDate diisi Mapster dari properti ringkasan Premi
        return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}
