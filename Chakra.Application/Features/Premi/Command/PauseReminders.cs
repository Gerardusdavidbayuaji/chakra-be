using Microsoft.EntityFrameworkCore;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Premi.Command;

public record PauseRemindersCommand(Guid PremiId) : IAdminRequest<Result<PremiResponseDto>>;

public class PauseRemindersCommandHandler : IRequestHandler<PauseRemindersCommand, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public PauseRemindersCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PremiResponseDto>> Handle(PauseRemindersCommand request, CancellationToken cancellationToken)
    {
        var premiId = new PremiId(request.PremiId);

        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.NotFound("Premi not found.");

        var adminId = _currentUser.DatabaseUserId;
        if (adminId == null)
            return Result<PremiResponseDto>.Unauthorized("Unauthorized");

        try
        {
            premi.PauseReminders(adminId.Value, _timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (DomainException ex)
        {
            return Result<PremiResponseDto>.Failure(ex.Message);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PremiResponseDto>.Failure("Premi was modified by another request, please retry.");
        }

        return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}
