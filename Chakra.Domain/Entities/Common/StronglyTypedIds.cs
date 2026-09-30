namespace Chakra.Domain.Entities.Common;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct PremiId(Guid Value)
{
    public static PremiId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct InstallmentId(Guid Value)
{
    public static InstallmentId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
