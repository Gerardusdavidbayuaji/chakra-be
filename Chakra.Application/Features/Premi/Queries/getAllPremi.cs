using Microsoft.EntityFrameworkCore;
using Gridify;
using Mapster;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Queries;

public class GetAllPremiQuery : PaginatedRequest, IAuthorizedRequest<Result<PaginatedResult<PremiResponseDto>>>;

public class GetAllPremiQueryHandler : IRequestHandler<GetAllPremiQuery, Result<PaginatedResult<PremiResponseDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAllPremiQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<PremiResponseDto>>> Handle(GetAllPremiQuery request, CancellationToken cancellationToken)
    {
        var gridifyQuery = request.ToGridifyQuery();

        // Cicilan dimuat agar ringkasan (InstallmentsPaid, RemainingAmount, NextDueDate) terisi
        var query = _context.Premis
            .AsNoTracking()
            .Include(p => p.Installments)
            .AsQueryable();

        if (!_currentUser.IsAdmin)
        {
            // Non-null: AuthorizationBehavior sudah menolak request tanpa login
            var userId = _currentUser.DatabaseUserId!.Value;
            query = query.Where(p => p.UserId == userId);
        }

        query = query.ApplyFilteringAndOrdering(gridifyQuery);

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
