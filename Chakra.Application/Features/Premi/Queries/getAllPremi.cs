using Microsoft.EntityFrameworkCore;
using Gridify;
using Mapster;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Queries;

public class GetAllPremiQuery : PaginatedRequest, IRequest<Result<PaginatedResult<PremiResponseDto>>>;

public class GetAllPremiQueryHandler : IRequestHandler<GetAllPremiQuery, Result<PaginatedResult<PremiResponseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllPremiQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedResult<PremiResponseDto>>> Handle(GetAllPremiQuery request, CancellationToken cancellationToken)
    {
        var gridifyQuery = request.ToGridifyQuery();

        var query = _context.Premis
            .AsNoTracking()
            .ApplyFilteringAndOrdering(gridifyQuery);

        var count = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((gridifyQuery.Page - 1) * gridifyQuery.PageSize)
            .Take(gridifyQuery.PageSize)
            .ToListAsync(cancellationToken);

        var data = items.Adapt<IEnumerable<PremiResponseDto>>()!;

        return Result<PaginatedResult<PremiResponseDto>>.Success(new PaginatedResult<PremiResponseDto>
        {
            Data = data,
            Count = count,
            Page = gridifyQuery.Page,
            PageSize = gridifyQuery.PageSize,
            TotalPages = (int)Math.Ceiling(count / (double)gridifyQuery.PageSize)
        });
    }
}