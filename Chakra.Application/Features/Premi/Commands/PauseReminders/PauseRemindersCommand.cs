using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Commands.PauseReminders;

public record PauseRemindersCommand(Guid PremiId) : IAdminRequest<Result<PremiResponseDto>>;
