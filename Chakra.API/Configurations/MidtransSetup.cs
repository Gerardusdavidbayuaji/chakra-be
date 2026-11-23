using Chakra.Application.Common;
using Chakra.Application.Features.Midtrans.Settings;
using Chakra.Application.Services;
using Chakra.Infrastructure.BackgroundJobs;
using Chakra.Infrastructure.Services.Midtrans;

namespace Chakra.API.Configurations;

public static class MidtransSetup
{
    public static IServiceCollection AddMidtransSetup(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MidtransSettings>(configuration.GetSection(MidtransSettings.SectionName));

        services.AddHttpClient<IMidtransClient, MidtransClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IPaymentLinkService, PaymentLinkService>();

        services.AddHostedService<PaymentExpiryProcessor>();

        return services;
    }
}
