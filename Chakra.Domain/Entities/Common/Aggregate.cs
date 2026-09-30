using System.ComponentModel.DataAnnotations.Schema;

namespace Chakra.Domain.Entities.Common;

public abstract class Aggregate<TId> : AuditableEntity<TId>, IAggregate
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
