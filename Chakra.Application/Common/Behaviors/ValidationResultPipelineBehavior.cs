using Chakra.Application.Common;
using FluentValidation;
using MediatR;

namespace Chakra.Application.Common.Behaviors;

public class ValidationResultPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationResultPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var errors = failures.Select(f => new ValidationError
            {
                Field = f.PropertyName,
                Message = f.ErrorMessage
            }).ToList();

            var validationFailureMethod = responseType.GetMethod(nameof(Result<object>.ValidationFailure))!;
            return (TResponse)validationFailureMethod.Invoke(null, new object[] { errors })!;
        }

        throw new ValidationException(failures);
    }
}