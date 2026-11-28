using Microsoft.EntityFrameworkCore;
using Gridify;
using Mapster;
using MediatR;
using Chakra.Application.Common;
using Chakra.Application.Features.Installments.Dtos;
using Chakra.Domain.Entities.Common;

namespace Chakra.Application.Features.Installments.Queries.GetAllInstallmentByPremiId;

internal sealed class GetAllInstallmentByPremiIdQueryHandler
    : IRequestHandler<GetAllInstallmentByPremiIdQuery, Result<PaginatedResult<InstallmentResponseDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAllInstallmentByPremiIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<InstallmentResponseDto>>> Handle(
        GetAllInstallmentByPremiIdQuery request, CancellationToken cancellationToken)
    {
        var premiId = new PremiId(request.PremiId);
        var isAdmin = _currentUser.IsAdmin;
        var userId = _currentUser.DatabaseUserId!.Value;

        // Premi milik orang lain dijawab sama dengan tidak ada
        var premiVisible = await _context.Premis
            .AnyAsync(p => p.Id == premiId && (isAdmin || p.UserId == userId), cancellationToken);
        if (!premiVisible)
            return Result<PaginatedResult<InstallmentResponseDto>>.NotFound("Premi not found.");

        var gridifyQuery = request.ToGridifyQuery();

        var query = _context.Installments
            .AsNoTracking()
            .Where(i => i.PremiId == premiId)
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
