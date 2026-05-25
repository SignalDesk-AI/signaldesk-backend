using System.Net.Sockets;
using System.Security.Cryptography;
using BuildingBlocks.Application.Context;
using BuildingBlocks.Application.Health;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure.Context;
using BuildingBlocks.Infrastructure.Observability;
using Microsoft.IdentityModel.Tokens;
using SignalDesk.Identity.Application;
using SignalDesk.Identity.Infrastructure;

const string serviceName = "identity-service";

// Ensure RSA key is available before any service reads it.
// Both IdentityTokenGenerator and /auth/me validation use IDENTITY_JWT_RSA_KEY.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_JWT_RSA_KEY")))
{
    var rsa = RSA.Create(2048);
    Environment.SetEnvironmentVariable("IDENTITY_JWT_RSA_KEY", rsa.ExportRSAPrivateKeyPem());
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddSingleton<ITraceContextAccessor, SystemDiagnosticsTraceContextAccessor>();

var identityConnectionString = builder.Configuration["IDENTITY_DB_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION_STRING")
    ?? "Host=localhost;Port=6432;Database=signaldesk;Username=signaldesk;Password=signaldesk";

builder.Services.AddIdentityInfrastructure(identityConnectionString);
builder.Services.AddScoped<IIdentityAuthService, IdentityAuthService>();

var app = builder.Build();

// Run identity schema migrations on startup so fresh databases have identity
// tables before any auth writes. Idempotent (uses IF NOT EXISTS).
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<SignalDesk.Identity.Infrastructure.Persistence.IdentityDatabaseMigrator>();
    await migrator.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Catch framework binding/parse failures and unexpected exceptions
// and return the shared error envelope.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var correlationContext = context.RequestServices.GetRequiredService<ICorrelationContext>();

        if (exception is BadHttpRequestException or System.Text.Json.JsonException
            or FormatException or OverflowException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = exception.Message,
                    details = Array.Empty<object>(),
                    correlationId = correlationContext.CorrelationId
                }
            });
        }
        else if (exception is not null)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An unexpected error occurred.",
                    details = Array.Empty<object>(),
                    correlationId = correlationContext.CorrelationId
                }
            });
        }
    });
});

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

app.MapPost("/auth/register", async (
    RegisterRequest request,
    IIdentityAuthService authService,
    ICorrelationContext correlationContext,
    CancellationToken ct) =>
{
    var command = new RegisterUserCommand(
        request.Email,
        request.Password,
        request.DisplayName,
        request.AvatarUrl,
        request.DeviceId,
        request.TenantId);

    var result = await authService.RegisterAsync(command, ct);

    if (result.IsFailure)
    {
        return ErrorEnvelope(result.Error!, correlationContext.CorrelationId);
    }

    var r = result.Value!;
    return Results.Ok(new
    {
        data = new
        {
            userId = r.UserId,
            email = r.Email,
            emailVerified = r.EmailVerified,
            emailVerificationToken = r.EmailVerificationToken,
            emailVerificationTokenExpiresAt = r.EmailVerificationTokenExpiresAt,
            tokens = MapTokenPair(r.Tokens)
        },
        meta = new { correlationId = correlationContext.CorrelationId }
    });
})
.WithName("Register")
.WithOpenApi();

app.MapPost("/auth/login", async (
    LoginRequest request,
    IIdentityAuthService authService,
    ICorrelationContext correlationContext,
    CancellationToken ct) =>
{
    var command = new LoginCommand(
        request.Email,
        request.Password,
        request.DeviceId,
        request.TenantId);

    var result = await authService.LoginAsync(command, ct);

    if (result.IsFailure)
    {
        return ErrorEnvelope(result.Error!, correlationContext.CorrelationId);
    }

    return Results.Ok(new
    {
        data = new { tokens = MapTokenPair(result.Value!.Tokens) },
        meta = new { correlationId = correlationContext.CorrelationId }
    });
})
.WithName("Login")
.WithOpenApi();

app.MapPost("/auth/refresh", async (
    RefreshRequest request,
    IIdentityAuthService authService,
    ICorrelationContext correlationContext,
    CancellationToken ct) =>
{
    var command = new RefreshTokenCommand(
        request.RefreshToken,
        request.DeviceId,
        request.TenantId);

    var result = await authService.RefreshAsync(command, ct);

    if (result.IsFailure)
    {
        return ErrorEnvelope(result.Error!, correlationContext.CorrelationId);
    }

    var refreshResult = result.Value!;
    if (refreshResult.Outcome != RefreshTokenOutcome.Valid)
    {
        return RefreshOutcomeError(refreshResult.Outcome, correlationContext.CorrelationId);
    }

    return Results.Ok(new
    {
        data = new { tokens = MapTokenPair(refreshResult.Tokens!) },
        meta = new { correlationId = correlationContext.CorrelationId }
    });
})
.WithName("Refresh")
.WithOpenApi();

app.MapPost("/auth/logout", async (
    LogoutRequest request,
    IIdentityAuthService authService,
    ICorrelationContext correlationContext,
    CancellationToken ct) =>
{
    var command = new LogoutCommand(request.RefreshToken);
    var result = await authService.LogoutAsync(command, ct);

    if (result.IsFailure)
    {
        return ErrorEnvelope(result.Error!, correlationContext.CorrelationId);
    }

    return Results.Ok(new
    {
        data = new { revoked = result.Value!.Revoked },
        meta = new { correlationId = correlationContext.CorrelationId }
    });
})
.WithName("Logout")
.WithOpenApi();

app.MapPost("/auth/verify-email", async (
    VerifyEmailRequest request,
    IIdentityAuthService authService,
    ICorrelationContext correlationContext,
    CancellationToken ct) =>
{
    var command = new VerifyEmailCommand(request.VerificationToken);
    var result = await authService.VerifyEmailAsync(command, ct);

    if (result.IsFailure)
    {
        return ErrorEnvelope(result.Error!, correlationContext.CorrelationId);
    }

    var r = result.Value!;
    return Results.Ok(new
    {
        data = new { userId = r.UserId, email = r.Email, verifiedAt = r.VerifiedAt },
        meta = new { correlationId = correlationContext.CorrelationId }
    });
})
.WithName("VerifyEmail")
.WithOpenApi();

app.MapPost("/auth/password-reset/request", async (
    RequestPasswordResetBody body,
    IIdentityAuthService authService,
    ICorrelationContext correlationContext,
    CancellationToken ct) =>
{
    var command = new RequestPasswordResetCommand(body.Email);
    var result = await authService.RequestPasswordResetAsync(command, ct);

    if (result.IsFailure)
    {
        return ErrorEnvelope(result.Error!, correlationContext.CorrelationId);
    }

    return Results.Ok(new
    {
        data = new { accepted = result.Value!.Accepted },
        meta = new { correlationId = correlationContext.CorrelationId }
    });
})
.WithName("RequestPasswordReset")
.WithOpenApi();

app.MapPost("/auth/password-reset/confirm", async (
    ConfirmPasswordResetBody body,
    IIdentityAuthService authService,
    ICorrelationContext correlationContext,
    CancellationToken ct) =>
{
    var command = new ConfirmPasswordResetCommand(body.ResetToken, body.NewPassword);
    var result = await authService.ConfirmPasswordResetAsync(command, ct);

    if (result.IsFailure)
    {
        return ErrorEnvelope(result.Error!, correlationContext.CorrelationId);
    }

    var r = result.Value!;
    return Results.Ok(new
    {
        data = new { userId = r.UserId, email = r.Email, passwordChangedAt = r.PasswordChangedAt },
        meta = new { correlationId = correlationContext.CorrelationId }
    });
})
.WithName("ConfirmPasswordReset")
.WithOpenApi();

app.MapGet("/auth/me", (
    HttpContext httpContext,
    ICorrelationContext correlationContext) =>
{
    var authorization = httpContext.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer "))
    {
        return Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Missing access token",
                    details = Array.Empty<object>(),
                    correlationId = correlationContext.CorrelationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var tokenValue = authorization["Bearer ".Length..].Trim();
    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

    if (!handler.CanReadToken(tokenValue))
    {
        return Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Invalid access token",
                    details = Array.Empty<object>(),
                    correlationId = correlationContext.CorrelationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var rsa = RSA.Create(2048);
    var rsaKeyPem = Environment.GetEnvironmentVariable("IDENTITY_JWT_RSA_KEY");
    if (!string.IsNullOrEmpty(rsaKeyPem))
    {
        rsa.ImportFromPem(rsaKeyPem);
    }

    var validationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "signaldesk-identity",
        ValidAudience = "signaldesk-api",
        IssuerSigningKey = new RsaSecurityKey(rsa) { KeyId = "identity-rs256-001" }
    };

    try
    {
        var principal = handler.ValidateToken(tokenValue, validationParameters, out _);
        // JwtSecurityTokenHandler maps "sub" to ClaimTypes.NameIdentifier by default.
        var userId = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst("email")?.Value;
        var emailVerified = principal.FindFirst("email_verified")?.Value == "true";
        var tenantId = principal.FindFirst("tid")?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "UNAUTHENTICATED",
                        message = "Access token is missing required claims",
                        details = Array.Empty<object>(),
                        correlationId = correlationContext.CorrelationId
                    }
                },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(new
        {
            data = new { userId, email, emailVerified, tenantId },
            meta = new { correlationId = correlationContext.CorrelationId }
        });
    }
    catch (SecurityTokenException)
    {
        return Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Access token is invalid or expired",
                    details = Array.Empty<object>(),
                    correlationId = correlationContext.CorrelationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized);
    }
})
.WithName("GetCurrentUser")
.WithOpenApi();

app.Run();

static IResult ErrorEnvelope(IdentityApplicationError error, string? correlationId)
{
    var statusCode = error.EnvelopeCode switch
    {
        IdentityEnvelopeErrorCodes.ValidationError => StatusCodes.Status400BadRequest,
        IdentityEnvelopeErrorCodes.Unauthenticated => StatusCodes.Status401Unauthorized,
        IdentityEnvelopeErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
        IdentityEnvelopeErrorCodes.NotFound => StatusCodes.Status404NotFound,
        IdentityEnvelopeErrorCodes.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    return Results.Json(
        new
        {
            error = new
            {
                code = error.EnvelopeCode,
                message = error.Message,
                details = new[] { new { code = error.Code } },
                correlationId
            }
        },
        statusCode: statusCode);
}

static IResult RefreshOutcomeError(RefreshTokenOutcome outcome, string? correlationId)
{
    return outcome switch
    {
        RefreshTokenOutcome.Invalid => Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Refresh token is invalid.",
                    details = new[] { new { code = IdentityApplicationErrorCodes.InvalidRefreshToken } },
                    correlationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized),
        RefreshTokenOutcome.Expired => Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Refresh token is expired.",
                    details = new[] { new { code = IdentityApplicationErrorCodes.RefreshTokenExpired } },
                    correlationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized),
        RefreshTokenOutcome.Revoked => Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Refresh token is revoked.",
                    details = new[] { new { code = IdentityApplicationErrorCodes.RefreshTokenRevoked } },
                    correlationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized),
        RefreshTokenOutcome.Replayed => Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Refresh token has already been used.",
                    details = new[] { new { code = IdentityApplicationErrorCodes.RefreshTokenReplayed } },
                    correlationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized),
        RefreshTokenOutcome.TenantMismatch => Results.Json(
            new
            {
                error = new
                {
                    code = "UNAUTHENTICATED",
                    message = "Refresh token tenant mismatch.",
                    details = new[] { new { code = IdentityApplicationErrorCodes.TenantMismatch } },
                    correlationId
                }
            },
            statusCode: StatusCodes.Status401Unauthorized),
        RefreshTokenOutcome.TenantMembershipRevoked => Results.Json(
            new
            {
                error = new
                {
                    code = "FORBIDDEN",
                    message = "Tenant membership is inactive or revoked.",
                    details = new[] { new { code = IdentityApplicationErrorCodes.TenantMembershipRevoked } },
                    correlationId
                }
            },
            statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Json(
            new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An unexpected error occurred.",
                    details = Array.Empty<object>(),
                    correlationId
                }
            },
            statusCode: StatusCodes.Status500InternalServerError)
    };
}

static object MapTokenPair(TokenPairResult tokens)
{
    return new
    {
        accessToken = tokens.AccessToken,
        accessTokenExpiresAt = tokens.AccessTokenExpiresAt,
        refreshToken = tokens.RefreshToken,
        refreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
        userId = tokens.UserId,
        email = tokens.Email,
        emailVerified = tokens.EmailVerified,
        tenantId = tokens.TenantId
    };
}

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

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string? AvatarUrl = null,
    string? DeviceId = null,
    Guid? TenantId = null);

public sealed record LoginRequest(
    string Email,
    string Password,
    string? DeviceId = null,
    Guid? TenantId = null);

public sealed record RefreshRequest(
    string RefreshToken,
    string? DeviceId = null,
    Guid? TenantId = null);

public sealed record LogoutRequest(string RefreshToken);

public sealed record VerifyEmailRequest(string VerificationToken);

public sealed record RequestPasswordResetBody(string Email);

public sealed record ConfirmPasswordResetBody(string ResetToken, string NewPassword);
