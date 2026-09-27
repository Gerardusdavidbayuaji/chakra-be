using MediatR;

namespace Chakra.Application.Common.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuthorizedRequest<TResponse>
{
    private readonly ICurrentUserService _currentUser;

    public AuthorizationBehavior(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_currentUser.DatabaseUserId == null)
        {
            var responseType = typeof(TResponse);
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var failureMethod = responseType.GetMethod(nameof(Result<object>.Failure))!;
                return (TResponse)failureMethod.Invoke(null, new object[] { "Unauthorized" })!;
            }

            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        return await next();
    }
}
