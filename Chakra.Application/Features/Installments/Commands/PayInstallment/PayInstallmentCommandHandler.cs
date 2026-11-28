using Microsoft.EntityFrameworkCore;
using MediatR;
using Chakra.Application.Common;
using Chakra.Application.Features.Installments.Dtos;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Installments.Commands.PayInstallment;

internal sealed class PayInstallmentCommandHandler : IRequestHandler<PayInstallmentCommand, Result<InstallmentResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public PayInstallmentCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Result<InstallmentResponseDto>> Handle(PayInstallmentCommand request, CancellationToken cancellationToken)
    {
        var installmentId = new InstallmentId(request.InstallmentId);

        var premiId = await _context.Installments
            .Where(i => i.Id == installmentId)
            .Select(i => (PremiId?)i.PremiId)
            .FirstOrDefaultAsync(cancellationToken);

        if (premiId == null)
            return Result<InstallmentResponseDto>.NotFound("Installment not found.");

        // Semua cicilan dimuat karena aturan bayar berurutan dan lunas otomatis butuh seluruh jadwal
        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId.Value, cancellationToken);

        if (premi == null)
            return Result<InstallmentResponseDto>.NotFound("Installment not found.");

        try
        {
            var installment = premi.MarkInstallmentPaid(installmentId, _timeProvider.GetUtcNow().UtcDateTime);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<InstallmentResponseDto>.Success(installment.ToResponseDto());
        }
        catch (DomainException ex)
        {
            return Result<InstallmentResponseDto>.Failure(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InstallmentResponseDto>.Failure("Installment was modified by another request, please retry.");
        }
    }
}
