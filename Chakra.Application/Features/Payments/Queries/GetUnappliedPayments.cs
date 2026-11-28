using Microsoft.EntityFrameworkCore;
using Gridify;
using MediatR;

using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Enums;

namespace Chakra.Application.Features.Payments.Queries;

public class GetUnappliedPaymentsQuery : PaginatedRequest, IAdminRequest<Result<PaginatedResult<UnappliedPaymentDto>>>;

public class GetUnappliedPaymentsQueryHandler
    : IRequestHandler<GetUnappliedPaymentsQuery, Result<PaginatedResult<UnappliedPaymentDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetUnappliedPaymentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedResult<UnappliedPaymentDto>>> Handle(
        GetUnappliedPaymentsQuery request, CancellationToken cancellationToken)
    {
        var gridifyQuery = request.ToGridifyQuery();

        var query = _context.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.Status == PaymentTransactionStatus.Unapplied)
            .ApplyFiltering(gridifyQuery);

        var count = await query.CountAsync(cancellationToken);

        // Default: dana yang paling baru diterima di atas
        query = string.IsNullOrWhiteSpace(gridifyQuery.OrderBy)
            ? query.OrderByDescending(t => t.SettledAt)
            : query.ApplyOrdering(gridifyQuery);

        var transactions = await query
            .Skip((gridifyQuery.Page - 1) * gridifyQuery.PageSize)
            .Take(gridifyQuery.PageSize)
            .ToListAsync(cancellationToken);

        // Premi + cicilan + user untuk halaman ini saja, dalam satu query
        var premiIds = transactions.Select(t => t.PremiId).Distinct().ToList();
        var premis = await _context.Premis
            .AsNoTracking()
            .Include(p => p.Installments)
            .Include(p => p.User)
            .Where(p => premiIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
        var premiById = premis.ToDictionary(p => p.Id);

        var data = new List<UnappliedPaymentDto>();
        foreach (var transaction in transactions)
        {
            var dto = new UnappliedPaymentDto();
            transaction.CopyTo(dto);

            if (premiById.TryGetValue(transaction.PremiId, out var premi))
            {
                dto.UserName = premi.User.Name;
                dto.UserEmail = premi.User.Email;
                dto.PremiStatus = premi.Status;
                dto.InstallmentNumber = premi.Installments
                    .FirstOrDefault(i => i.Id == transaction.InstallmentId)?.InstallmentNumber ?? 0;
                dto.CanAllocate = premi.Status == PremiStatus.Active
                                  && premi.NextUnpaidInstallment()?.Amount == transaction.Amount;
            }

            data.Add(dto);
        }

        return Result<PaginatedResult<UnappliedPaymentDto>>.Success(new PaginatedResult<UnappliedPaymentDto>
        {
            Data = data,
            Count = count,
            Page = gridifyQuery.Page,
            PageSize = gridifyQuery.PageSize,
            TotalPages = (int)Math.Ceiling(count / (double)gridifyQuery.PageSize)
        });
    }
}
