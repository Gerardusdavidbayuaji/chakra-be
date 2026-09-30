namespace Chakra.Domain.Entities.Common;

public interface IAggregate
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
