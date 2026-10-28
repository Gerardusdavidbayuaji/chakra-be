namespace Chakra.Application.Features.Outbox.Settings;

public class OutboxSettings
{
    public const string SectionName = "Outbox";

    public int IntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 5;
}
