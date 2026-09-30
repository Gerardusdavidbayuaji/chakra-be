using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Events;

public sealed record InstallmentPaidEvent(InstallmentId InstallmentId, PremiId PremiId) : IDomainEvent;
