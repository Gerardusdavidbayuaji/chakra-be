using Chakra.Application.Common;
using Chakra.Application.Features.Users.Dtos;

namespace Chakra.Application.Features.Users.Queries.GetUsers;

// Admin saja: berisi email dan chat id Telegram semua user
public record GetUsersQuery : IAdminRequest<Result<List<UserResponseDto>>>;
