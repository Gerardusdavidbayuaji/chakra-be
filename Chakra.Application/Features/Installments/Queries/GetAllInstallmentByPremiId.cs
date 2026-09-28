using Microsoft.EntityFrameworkCore;
using Gridify;
using Mapster;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Installments.Dtos;

namespace Chakra.Application.Features.Installments.Queries;

public class GetAllInstallmentByPremiIdQuery : PaginatedRequest, IRequest<Result<PaginatedResult<InstallmentResponseDto>>>
{
    public Guid PremiId { get; set; }
}

public class GetAllInstallmentByPremiIdQueryHandler
    : IRequestHandler<GetAllInstallmentByPremiIdQuery, Result<PaginatedResult<InstallmentResponseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllInstallmentByPremiIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedResult<InstallmentResponseDto>>> Handle(
        GetAllInstallmentByPremiIdQuery request, CancellationToken cancellationToken)
    {
        var premiExists = await _context.Premis.AnyAsync(p => p.Id == request.PremiId, cancellationToken);
        if (!premiExists)
            return Result<PaginatedResult<InstallmentResponseDto>>.Failure("Premi not  found.");

        var gridifyQuery = request.ToGridifyQuery();

        var query = _context.Installments
            .AsNoTracking()
            .Where(i => i.PremiId == request.PremiId)
            .ApplyFilteringAndOrdering(gridifyQuery);

        var count = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((gridifyQuery.Page - 1) * gridifyQuery.PageSize)
            .Take(gridifyQuery.PageSize)
            .ToListAsync(cancellationToken);

        var data = items.Adapt<IEnumerable<InstallmentResponseDto>>()!;

        return Result<PaginatedResult<InstallmentResponseDto>>.Success(new PaginatedResult<InstallmentResponseDto>
        {
            Data = data,
            Count = count,
            Page = gridifyQuery.Page,
            PageSize = gridifyQuery.PageSize,
            TotalPages = (int)Math.Ceiling(count / (double)gridifyQuery.PageSize)
        });
    }
}
