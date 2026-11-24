using System.Globalization;
using Chakra.Application.Features.Scheduler;
using Chakra.Application.Features.Scheduler.Settings;
using Chakra.Infrastructure.BackgroundJobs;

namespace Chakra.API.Configurations;

public static class SchedulerSetup
{
    public static IServiceCollection AddSchedulerSetup(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(SchedulerSettings.SectionName);
        services.Configure<SchedulerSettings>(section);

        Validate(section.Get<SchedulerSettings>() ?? new SchedulerSettings(), environment);

        services.AddScoped<DailyReminderService>();

        services.AddHostedService<DailyReminderProcessor>();

        return services;
    }

    private static void Validate(SchedulerSettings settings, IHostEnvironment environment)
    {
        if (settings.TimeScale == SchedulerTimeScale.Minutes
            && (!environment.IsDevelopment() || settings.SimulationStartDate == null))
            throw new InvalidOperationException(
                "Scheduler TimeScale 'Minutes' is only allowed in Development and requires SimulationStartDate.");

        if (settings.DaysBeforeSecond <= 0 || settings.DaysBeforeFirst <= settings.DaysBeforeSecond)
            throw new InvalidOperationException(
                "Scheduler reminder days are invalid: DaysBeforeFirst must be greater than DaysBeforeSecond, and DaysBeforeSecond must be greater than 0.");

        if (settings.RepeatEveryDays <= 0)
            throw new InvalidOperationException("Scheduler RepeatEveryDays must be greater than 0.");

        if (settings.AdminAlertAfterDueDays < 0)
            throw new InvalidOperationException("Scheduler AdminAlertAfterDueDays must be 0 or greater.");

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"Scheduler TimeZone '{settings.TimeZone}' could not be resolved on this system.", ex);
        }

        if (!TimeOnly.TryParse(settings.RunAtLocalTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidOperationException(
                $"Scheduler RunAtLocalTime '{settings.RunAtLocalTime}' is not a valid time (expected HH:mm).");
    }
}
