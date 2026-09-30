namespace Chakra.Domain.Entities.Common;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}

public abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity
    where TId : struct
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
