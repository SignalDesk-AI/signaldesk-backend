using System.Net.Sockets;

const string serviceName = "support-service";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = serviceName,
    checkedAt = DateTimeOffset.UtcNow
}))
.WithName("LiveHealth")
.WithOpenApi();

app.MapGet("/health/ready", async () =>
{
    var dependency = await CheckTcpDependencyAsync(
        "pgbouncer",
        GetEnv("PGBOUNCER_HOST", "localhost"),
        GetIntEnv("PGBOUNCER_PORT", 6432));
    var response = new
    {
        status = dependency.Status == "ok" ? "ok" : "degraded",
        service = serviceName,
        checkedAt = DateTimeOffset.UtcNow,
        dependencies = new[] { dependency }
    };

    return dependency.Status == "ok"
        ? Results.Ok(response)
        : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
})
.WithName("ReadyHealth")
.WithOpenApi();

app.Run();

static string GetEnv(string name, string fallback)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value) ? fallback : value;
}

static int GetIntEnv(string name, int fallback)
{
    var value = Environment.GetEnvironmentVariable(name);
    return int.TryParse(value, out var parsed) ? parsed : fallback;
}

static async Task<DependencyCheck> CheckTcpDependencyAsync(
    string name,
    string host,
    int port,
    int timeoutMs = 1500)
{
    using var client = new TcpClient();
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
    var target = $"{host}:{port}";

    try
    {
        await client.ConnectAsync(host, port, cts.Token);

        return new DependencyCheck(
            name,
            "ok",
            target,
            true,
            DateTimeOffset.UtcNow,
            null);
    }
    catch (Exception exception)
    {
        return new DependencyCheck(
            name,
            "fail",
            target,
            true,
            DateTimeOffset.UtcNow,
            exception.Message);
    }
}

public sealed record DependencyCheck(
    string Name,
    string Status,
    string Target,
    bool Required,
    DateTimeOffset CheckedAt,
    string? Error);
