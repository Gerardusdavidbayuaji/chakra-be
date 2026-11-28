using Chakra.Application.Mappers;

namespace Chakra.API.Configurations;

public static class ApplicationSetup
{
    public static IServiceCollection AddApplicationSetup(this IServiceCollection services,  IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();

        MapsterConfig.RegisterMappings();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
