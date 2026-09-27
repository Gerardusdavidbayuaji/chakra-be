using Microsoft.EntityFrameworkCore;
using MediatR;
using Mapster;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Domain.Entities.Enums;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Queries;

public record GetPremiByIdQuery(Guid Id) : IRequest<Result<PremiResponseDto>>;

public class GetPremiByIdQueryHandler : IRequestHandler<GetPremiByIdQuery, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPremiByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PremiResponseDto>> Handle(GetPremiByIdQuery request, CancellationToken cancellationToken)
    {
        var premi = await _context.Premis
            .AsNoTracking()
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.Failure("Premi not found.");
        
        var installmentsPaid = premi.Installments
            .Count(i => i.Status == InstallmentStatus.Paid);

        var paidAmount = premi.InstallmentAmount * installmentsPaid;

        var nextDue = premi.Installments
            .Where(i => i.Status != InstallmentStatus.Paid)
            .OrderBy(i => i.DueDate)
            .FirstOrDefault();
        
        var dto = premi.Adapt<PremiResponseDto>();
        dto.InstallmentsPaid = installmentsPaid;
        dto.RemainingAmount = premi.TotalAmount - paidAmount;
        dto.NextDueDate = nextDue?.DueDate;
        
        return Result<PremiResponseDto>.Success(dto);
    }
}