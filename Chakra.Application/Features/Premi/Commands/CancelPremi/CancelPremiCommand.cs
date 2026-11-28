using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Commands.CancelPremi;

public record CancelPremiCommand(Guid Id) : IAdminRequest<Result<PremiResponseDto>>;
