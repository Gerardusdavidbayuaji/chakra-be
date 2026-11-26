using Chakra.Domain.Entities.Common;

namespace Chakra.Domain.Events;

public sealed record PremiCancelledEvent(PremiId PremiId) : IDomainEvent;
