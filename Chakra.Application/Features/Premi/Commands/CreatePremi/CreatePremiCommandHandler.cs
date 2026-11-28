using Microsoft.EntityFrameworkCore;
using MediatR;
using PremiEntity = Chakra.Domain.Entities.Premi;
using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Mappers;
using Chakra.Application.Common;
using Chakra.Domain.Entities.Common;
using Chakra.Domain.Exceptions;

namespace Chakra.Application.Features.Premi.Commands.CreatePremi;

internal sealed class CreatePremiCommandHandler : IRequestHandler<CreatePremiCommand, Result<PremiResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public CreatePremiCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PremiResponseDto>> Handle(CreatePremiCommand request, CancellationToken cancellationToken)
    {
            var userId = new UserId(request.UserId);

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userExists)
                return Result<PremiResponseDto>.NotFound("User not found");

            PremiEntity premi;
            try
            {
                premi = PremiEntity.Create(userId, request.TotalAmount, request.Tenor, request.DueDay,
                    request.GracePeriodDays, request.StartDate);
            }
            catch (DomainException ex)
            {
                return Result<PremiResponseDto>.Failure(ex.Message);
            }

            _context.Premis.Add(premi);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<PremiResponseDto>.Success(premi.ToResponseDto());
    }
}
