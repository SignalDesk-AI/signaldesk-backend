const string serviceName = "identity-service";

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

app.MapGet("/health/ready", () => Results.Ok(new
{
    status = "ok",
    service = serviceName,
    checkedAt = DateTimeOffset.UtcNow
}))
.WithName("ReadyHealth")
.WithOpenApi();

app.Run();
