using Chakra.Application.Common;
using Chakra.Application.Features.Installments.Dtos;

namespace Chakra.Application.Features.Installments.Queries.GetAllInstallmentByPremiId;

public class GetAllInstallmentByPremiIdQuery : PaginatedRequest, IAuthorizedRequest<Result<PaginatedResult<InstallmentResponseDto>>>
{
    public Guid PremiId { get; set; }
}
