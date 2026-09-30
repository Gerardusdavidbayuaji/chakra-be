using Chakra.Domain.Entities.Common;
using Mapster;

namespace Chakra.Application.Mappers;

public static class MapsterConfig
{
    public static void RegisterMappings()
    {
        TypeAdapterConfig<UserId, Guid>.NewConfig().MapWith(id => id.Value);
        TypeAdapterConfig<PremiId, Guid>.NewConfig().MapWith(id => id.Value);
        TypeAdapterConfig<InstallmentId, Guid>.NewConfig().MapWith(id => id.Value);
    }
}
