using Microsoft.Extensions.DependencyInjection;
using SignalDesk.Identity.Application;
using SignalDesk.Identity.Infrastructure.Cleanup;
using SignalDesk.Identity.Infrastructure.Persistence;
using SignalDesk.Identity.Infrastructure.Security;
using SignalDesk.Identity.Infrastructure.Tokens;
using SignalDesk.Identity.Infrastructure.Workspace;

namespace SignalDesk.Identity.Infrastructure;

public static class IdentityInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers all identity infrastructure adapters with PostgreSQL persistence.
    /// TransactionBoundIdentityService is registered as Scoped and implements
    /// IIdentityUnitOfWork, IIdentityRepository, IIdentityOutboxWriter, and
    /// IRefreshTokenRotator — all sharing one database connection/transaction
    /// per request scope.
    /// </summary>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Database connection factory (singleton, stateless)
        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));

        // Migration runner (singleton, call MigrateAsync at startup)
        services.AddSingleton<IdentityDatabaseMigrator>();

        // Clock (singleton, stateless)
        services.AddSingleton<IIdentityClock, SystemUtcClock>();

        // Security (singleton, stateless)
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenHashService, SHA256TokenHashService>();

        // Token generation (singleton, holds RSA key)
        services.AddSingleton<IIdentityTokenGenerator, IdentityTokenGenerator>();

        // TransactionBoundIdentityService — SCOPED, one connection+transaction per request.
        // Implements IIdentityUnitOfWork, IIdentityRepository, IIdentityOutboxWriter,
        // and IRefreshTokenRotator. IdentityAuthService resolves all four from this
        // single instance, ensuring all operations share one PostgreSQL transaction.
        services.AddScoped<TransactionBoundIdentityService>();
        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<TransactionBoundIdentityService>());
        services.AddScoped<IIdentityRepository>(sp => sp.GetRequiredService<TransactionBoundIdentityService>());
        services.AddScoped<IIdentityOutboxWriter>(sp => sp.GetRequiredService<TransactionBoundIdentityService>());
        services.AddScoped<IRefreshTokenRotator>(sp => sp.GetRequiredService<TransactionBoundIdentityService>());

        // Workspace membership: prefer real HTTP reader when workspace-service URL
        // is configured. Falls back to FakeWorkspaceMembershipReader (always active)
        // for local development/testing when no URL is set.
        // Day 5 integration: set WORKSPACE_SERVICE_URL to the workspace-service
        // internal endpoint to enforce real membership checks.
        var workspaceServiceUrl = Environment.GetEnvironmentVariable("WORKSPACE_SERVICE_URL");
        if (!string.IsNullOrWhiteSpace(workspaceServiceUrl))
        {
            services.AddHttpClient<IWorkspaceMembershipReader, HttpWorkspaceMembershipReader>(client =>
            {
                client.BaseAddress = new Uri(workspaceServiceUrl);
            });
        }
        else
        {
            services.AddSingleton<IWorkspaceMembershipReader, FakeWorkspaceMembershipReader>();
        }

        // Cleanup (singleton, creates its own connection independent of request scope)
        services.AddSingleton<IdentityTokenCleanupService>();

        return services;
    }

    /// <summary>
    /// Registers identity infrastructure with the real HTTP workspace membership reader.
    /// Use this overload after Day 5 workspace-service is available.
    /// </summary>
    public static IServiceCollection AddIdentityInfrastructureWithHttpWorkspace(
        this IServiceCollection services,
        string connectionString,
        string workspaceServiceUrl)
    {
        services.AddIdentityInfrastructure(connectionString);

        // Replace fake with real HTTP workspace membership reader.
        var fakeDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IWorkspaceMembershipReader));
        if (fakeDescriptor is not null)
        {
            services.Remove(fakeDescriptor);
        }

        services.AddHttpClient<IWorkspaceMembershipReader, HttpWorkspaceMembershipReader>(client =>
        {
            client.BaseAddress = new Uri(workspaceServiceUrl);
        });

        return services;
    }
}
