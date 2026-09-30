using Microsoft.EntityFrameworkCore;
using MediatR;

using Chakra.Application.Features.Installments.Dtos;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities.Common;


namespace Chakra.Application.Features.Installments.Queries;

public record GetInstallmentByIdQuery(Guid Id) : IRequest<Result<InstallmentResponseDto>>;

public class GetInstallmentByIdQueryHandler
    : IRequestHandler<GetInstallmentByIdQuery, Result<InstallmentResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInstallmentByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<InstallmentResponseDto>> Handle(
        GetInstallmentByIdQuery request, CancellationToken cancellationToken)
    {
        var installmentId = new InstallmentId(request.Id);

        var installment = await _context.Installments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == installmentId, cancellationToken);

        if (installment == null)
            return Result<InstallmentResponseDto>.Failure("Installment not found.");

        return Result<InstallmentResponseDto>.Success(installment.ToResponseDto());
    }
}