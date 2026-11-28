using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Queries.GetAllPremi;

public class GetAllPremiQuery : PaginatedRequest, IAuthorizedRequest<Result<PaginatedResult<PremiResponseDto>>>;
