using Chakra.Application;
using FluentValidation;

namespace Chakra.API.Configurations;

public static class ValidationSetup
{
    public static IServiceCollection AddValidationSetup(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyReference).Assembly);

        return services;
    }
}