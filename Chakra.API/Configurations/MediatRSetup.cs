using Chakra.Application;
using Chakra.Application.Common.Behaviors;

namespace Chakra.API.Configurations;

public static class MediatRSetup
{
    public static IServiceCollection AddMediatRSetup(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyReference).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationResultPipelineBehavior<,>));
        });

        return services;
    }
}
