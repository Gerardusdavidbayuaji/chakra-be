using Microsoft.EntityFrameworkCore;
using MediatR;
using Chakra.Application.Common;
using Chakra.Application.Features.Payments.Dtos;
using Chakra.Application.Services;
using Chakra.Domain.Entities.Common;

namespace Chakra.Application.Features.Payments.Commands.CreatePaymentLink;

internal sealed class CreatePaymentLinkCommandHandler : IRequestHandler<CreatePaymentLinkCommand, Result<PaymentLinkDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentLinkService _paymentLinkService;

    public CreatePaymentLinkCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPaymentLinkService paymentLinkService)
    {
        _context = context;
        _currentUser = currentUser;
        _paymentLinkService = paymentLinkService;
    }

    public async Task<Result<PaymentLinkDto>> Handle(CreatePaymentLinkCommand request, CancellationToken cancellationToken)
    {
        var installmentId = new InstallmentId(request.InstallmentId);

        var ownerId = await _context.Installments
            .AsNoTracking()
            .Where(i => i.Id == installmentId)
            .Select(i => (UserId?)i.Premi.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        // Cicilan milik orang lain dijawab sama dengan tidak ada
        if (ownerId == null || (!_currentUser.IsAdmin && ownerId != _currentUser.DatabaseUserId))
            return Result<PaymentLinkDto>.NotFound("Installment not found.");

        return await _paymentLinkService.GetOrCreateAsync(installmentId, cancellationToken);
    }
}
