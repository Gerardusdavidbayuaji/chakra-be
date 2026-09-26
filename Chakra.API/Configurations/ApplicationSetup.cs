// using Chakra.Application.Features.Midtrans.Settings;
// using Chakra.Application.Services;
// using Chakra.Application.Services;

namespace Chakra.API.Configurations;

public static class ApplicationSetup
{
    public static IServiceCollection AddApplicationSetup(this IServiceCollection services,  IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        // services.AddScoped<PremiCompletionService>();
        // services.Configure<MidtransSettings>(configuration.GetSection(MidtransSettings.SectionsName));

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
