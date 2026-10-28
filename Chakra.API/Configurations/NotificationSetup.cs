using Chakra.Application.Common;
using Chakra.Application.Features.Outbox.Settings;
using Chakra.Application.Features.Telegram.Settings;
using Chakra.Infrastructure.BackgroundJobs;
using Chakra.Infrastructure.Services.Telegram;

namespace Chakra.API.Configurations;

public static class NotificationSetup
{
    public static IServiceCollection AddNotificationSetup(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName));
        services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.SectionName));

        services.AddHttpClient<ITelegramClient, TelegramClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
