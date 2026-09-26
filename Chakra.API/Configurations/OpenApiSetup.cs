using Scalar.AspNetCore;

namespace Chakra.API.Configurations;

public static class OpenApiSetup
{
    public static IServiceCollection AddOpenApiSetup(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

    public static WebApplication UseOpenApiSetup(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "ChakraApp API";
        });
        return app;
    }
}
