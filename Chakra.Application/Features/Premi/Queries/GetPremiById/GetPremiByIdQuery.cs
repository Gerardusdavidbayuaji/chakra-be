using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Queries.GetPremiById;

public record GetPremiByIdQuery(Guid Id) : IAuthorizedRequest<Result<PremiResponseDto>>;
