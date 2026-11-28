using Chakra.Application.Features.Installments.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Installments.Queries.GetInstallmentById;

public record GetInstallmentByIdQuery(Guid Id) : IAuthorizedRequest<Result<InstallmentResponseDto>>;
