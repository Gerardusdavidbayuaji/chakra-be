using MediatR;

namespace Chakra.Application.Common.Behaviors;

// Didaftarkan setelah AuthorizationBehavior, jadi user di sini sudah pasti login
public class AdminAuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAdminRequest<TResponse>
{
    private readonly ICurrentUserService _currentUser;

    public AdminAuthorizationBehavior(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
        {
            var responseType = typeof(TResponse);
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var forbiddenMethod = responseType.GetMethod(nameof(Result<object>.Forbidden))!;
                return (TResponse)forbiddenMethod.Invoke(null, new object[] { "Forbidden" })!;
            }

            throw new UnauthorizedAccessException("User is not an admin.");
        }

        return await next();
    }
}
