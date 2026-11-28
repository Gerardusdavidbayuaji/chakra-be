using Chakra.Application.Common;
using MediatR;
using Chakra.Application.Features.Users.Queries.GetUsers;

namespace Chakra.API.Endpoints;

public static class UserEndpoint
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/", async (IMediator mediator) =>
            {
                var result = await mediator.Send(new GetUsersQuery());
                return result.IsSuccess
                    ? Results.Ok(ApiResponse<object>.Success(result.Data!, 200, "Data retrieved successfully"))
                    : result.ToErrorResult();
            })
            .WithName("Get Users");
    }
}
