using Chakra.Application.Features.Premi.Dtos;
using Chakra.Application.Common;

namespace Chakra.Application.Features.Premi.Commands.ResumeReminders;

public record ResumeRemindersCommand(Guid PremiId) : IAdminRequest<Result<PremiResponseDto>>;
