namespace BuildingBlocks.Application.Health;

public sealed record HealthResponse(
    string Status,
    string Service,
    DateTimeOffset CheckedAt,
    IReadOnlyCollection<DependencyHealth> Dependencies)
{
    public static HealthResponse Live(string service)
    {
        return new HealthResponse("ok", service, DateTimeOffset.UtcNow, Array.Empty<DependencyHealth>());
    }

    public static HealthResponse Ready(string service, IReadOnlyCollection<DependencyHealth> dependencies)
    {
        var status = dependencies.All(dependency => dependency.Status == "ok" || !dependency.Required)
            ? "ok"
            : "degraded";

        return new HealthResponse(status, service, DateTimeOffset.UtcNow, dependencies);
    }
}

public sealed record DependencyHealth(
    string Name,
    string Status,
    string Target,
    bool Required,
    DateTimeOffset CheckedAt,
    string? Error = null);
