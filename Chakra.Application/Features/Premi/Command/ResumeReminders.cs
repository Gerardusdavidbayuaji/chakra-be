using Microsoft.EntityFrameworkCore;
using MediatR;

using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;
using Chakra.Application.Mappers;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Premi.Command;

public record ResumeRemindersCommand(Guid PremiId) : IAdminRequest<Result<PremiResponseDto>>;

public class ResumeRemindersCommandHandler : IRequestHandler<ResumeRemindersCommand, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ResumeRemindersCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PremiResponseDto>> Handle(ResumeRemindersCommand request, CancellationToken cancellationToken)
    {
        var premiId = new PremiId(request.PremiId);

        var premi = await _context.Premis
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == premiId, cancellationToken);

        if (premi == null)
            return Result<PremiResponseDto>.NotFound("Premi not found.");

        if (_currentUser.DatabaseUserId == null)
            return Result<PremiResponseDto>.Unauthorized("Unauthorized");

        try
        {
            premi.ResumeReminders();
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
