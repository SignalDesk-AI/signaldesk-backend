using Npgsql;

namespace SignalDesk.Identity.Infrastructure.Persistence;

// Runs service-owned identity schema migrations. Idempotent (uses IF NOT EXISTS).
// The migration SQL is embedded as a resource or read from the Migrations folder.
public sealed class IdentityDatabaseMigrator
{
    private readonly IDbConnectionFactory _connectionFactory;

    public IdentityDatabaseMigrator(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await RunMigrationAsync(connection, MigrationScripts.CreateIdentitySchema, cancellationToken);
    }

    private static async Task RunMigrationAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

internal static class MigrationScripts
{
    internal const string CreateIdentitySchema = """
        CREATE SCHEMA IF NOT EXISTS identity;

        CREATE TABLE IF NOT EXISTS identity.users (
            id uuid PRIMARY KEY,
            email varchar(320) NOT NULL,
            password_hash varchar(200) NOT NULL,
            display_name varchar(200) NOT NULL,
            avatar_url varchar(500) NULL,
            status varchar(30) NOT NULL DEFAULT 'Pending',
            email_verified boolean NOT NULL DEFAULT false,
            last_login_at timestamptz NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email ON identity.users (LOWER(email));

        CREATE TABLE IF NOT EXISTS identity.refresh_tokens (
            id uuid PRIMARY KEY,
            user_id uuid NOT NULL REFERENCES identity.users(id),
            tenant_id uuid NULL,
            token_hash varchar(128) NOT NULL,
            device_id varchar(200) NULL,
            expires_at timestamptz NOT NULL,
            revoked_at timestamptz NULL,
            replaced_by_token_id uuid NULL,
            created_at timestamptz NOT NULL
        );

        ALTER TABLE identity.refresh_tokens
            ADD COLUMN IF NOT EXISTS replaced_by_token_id uuid NULL;

        CREATE UNIQUE INDEX IF NOT EXISTS ix_refresh_tokens_token_hash ON identity.refresh_tokens (token_hash);
        CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id ON identity.refresh_tokens (user_id);
        CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expires_at_active ON identity.refresh_tokens (expires_at) WHERE revoked_at IS NULL;
        CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_tenant_active ON identity.refresh_tokens (user_id, tenant_id) WHERE revoked_at IS NULL;

        CREATE TABLE IF NOT EXISTS identity.email_verification_tokens (
            id uuid PRIMARY KEY,
            user_id uuid NOT NULL REFERENCES identity.users(id),
            token_hash varchar(128) NOT NULL,
            expires_at timestamptz NOT NULL,
            consumed_at timestamptz NULL,
            created_at timestamptz NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_email_verification_tokens_token_hash ON identity.email_verification_tokens (token_hash);
        CREATE INDEX IF NOT EXISTS ix_email_verification_tokens_user_id ON identity.email_verification_tokens (user_id);

        CREATE TABLE IF NOT EXISTS identity.password_reset_tokens (
            id uuid PRIMARY KEY,
            user_id uuid NOT NULL REFERENCES identity.users(id),
            token_hash varchar(128) NOT NULL,
            expires_at timestamptz NOT NULL,
            consumed_at timestamptz NULL,
            created_at timestamptz NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_password_reset_tokens_token_hash ON identity.password_reset_tokens (token_hash);
        CREATE INDEX IF NOT EXISTS ix_password_reset_tokens_user_id ON identity.password_reset_tokens (user_id);
        """;
}
