using Chakra.Application.Features.Users.Queries;
using MediatR;

namespace Chakra.API.Endpoints;

public static class UserEndpoint
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        group.MapGet("/", async (IMediator mediator) =>
            {
                var result = await mediator.Send(new GetUsersQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.Error);
            })
            .WithName("Get Users");
    }
}
