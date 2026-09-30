using Chakra.Application.Common;
using Chakra.Application.Features.Users.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chakra.Application.Features.Users.Queries;

public record GetUsersQuery : IRequest<Result<List<UserResponseDto>>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<List<UserResponseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<UserResponseDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Mapping di memori karena EF tidak bisa menerjemahkan .Value pada kolom ber-converter
        var data = users
            .Select(u => new UserResponseDto
            {
                Id = u.Id.Value,
                Name = u.Name,
                Email = u.Email,
                ChatId = u.ChatId,
                CreatedAt = u.CreatedAt
            })
            .ToList();

        return Result<List<UserResponseDto>>.Success(data);
    }
}