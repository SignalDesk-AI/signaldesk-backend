namespace BuildingBlocks.Application.Health;

public static class HealthChecks
{
    public static DependencyHealth Configured(string name, string? value, string target = "configuration")
    {
        var configured = !string.IsNullOrWhiteSpace(value);

        return new DependencyHealth(
            name,
            configured ? "ok" : "fail",
            target,
            true,
            DateTimeOffset.UtcNow,
            configured ? null : "Missing configuration value");
    }
}
