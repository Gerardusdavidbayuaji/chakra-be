namespace Chakra.Domain.Entities.Common;

public interface IEntity<out TId>
{
    TId Id { get; }
}
