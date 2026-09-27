using MediatR;

namespace Chakra.Application.Common;

public interface IAuthorizedRequest<out TResponse> : IRequest<TResponse>;