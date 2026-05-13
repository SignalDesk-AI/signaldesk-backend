using System.Net.Sockets;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.Health;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure.Context;
using BuildingBlocks.Infrastructure.Observability;

const string serviceName = "workspace-service";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddSingleton<ITraceContextAccessor, SystemDiagnosticsTraceContextAccessor>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(HealthResponse.Live(serviceName)))
.WithName("LiveHealth")
.WithOpenApi();

app.MapGet("/health/ready", async () =>
{
    var dependency = await CheckTcpDependencyAsync(
        "pgbouncer",
        GetEnv("PGBOUNCER_HOST", "localhost"),
        GetIntEnv("PGBOUNCER_PORT", 6432));
    var response = HealthResponse.Ready(serviceName, new[] { dependency });

    return response.Status == "ok"
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

static async Task<DependencyHealth> CheckTcpDependencyAsync(
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

        return new DependencyHealth(
            name,
            "ok",
            target,
            true,
            DateTimeOffset.UtcNow,
            null);
    }
    catch (Exception exception)
    {
        return new DependencyHealth(
            name,
            "fail",
            target,
            true,
            DateTimeOffset.UtcNow,
            exception.Message);
    }
}
