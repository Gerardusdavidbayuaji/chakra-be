using Microsoft.EntityFrameworkCore;
using MediatR;
using Chakra.Application.Features.Installments.Dtos;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities.Common;

namespace Chakra.Application.Features.Installments.Queries.GetInstallmentById;

internal sealed class GetInstallmentByIdQueryHandler
    : IRequestHandler<GetInstallmentByIdQuery, Result<InstallmentResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetInstallmentByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<InstallmentResponseDto>> Handle(
        GetInstallmentByIdQuery request, CancellationToken cancellationToken)
    {
        var installmentId = new InstallmentId(request.Id);

        var installment = await _context.Installments
            .AsNoTracking()
            .Include(i => i.Premi)
            .FirstOrDefaultAsync(i => i.Id == installmentId, cancellationToken);

        // Cicilan milik orang lain dijawab sama dengan tidak ada
        if (installment == null
            || (!_currentUser.IsAdmin && installment.Premi.UserId != _currentUser.DatabaseUserId))
            return Result<InstallmentResponseDto>.NotFound("Installment not found.");

        return Result<InstallmentResponseDto>.Success(installment.ToResponseDto());
    }
}
