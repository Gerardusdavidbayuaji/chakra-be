using Chakra.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chakra.Infrastructure.Common;

public sealed class UserIdConverter : ValueConverter<UserId, Guid>
{
    public UserIdConverter() : base(id => id.Value, value => new UserId(value))
    {
    }
}

public sealed class PremiIdConverter : ValueConverter<PremiId, Guid>
{
    public PremiIdConverter() : base(id => id.Value, value => new PremiId(value))
    {
    }
}

public sealed class InstallmentIdConverter : ValueConverter<InstallmentId, Guid>
{
    public InstallmentIdConverter() : base(id => id.Value, value => new InstallmentId(value))
    {
    }
}
