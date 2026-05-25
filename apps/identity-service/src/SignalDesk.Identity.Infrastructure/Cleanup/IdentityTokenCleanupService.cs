using Npgsql;
using SignalDesk.Identity.Infrastructure.Persistence;

namespace SignalDesk.Identity.Infrastructure.Cleanup;

// Idempotent cleanup service for expired refresh tokens, email verification tokens,
// and password reset tokens. Safe to run more than once (deletes only expired rows).
// Creates its own connection independent of the request-scoped unit of work.
public sealed class IdentityTokenCleanupService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public IdentityTokenCleanupService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IdentityTokenCleanupResult> CleanupExpiredTokensAsync(
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        var expiredBefore = now ?? DateTimeOffset.UtcNow;

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var refreshDeleted = await DeleteExpiredAsync(connection, transaction,
                "identity.refresh_tokens", expiredBefore, cancellationToken);

            var emailVerificationDeleted = await DeleteExpiredAsync(connection, transaction,
                "identity.email_verification_tokens", expiredBefore, cancellationToken);

            var passwordResetDeleted = await DeleteExpiredAsync(connection, transaction,
                "identity.password_reset_tokens", expiredBefore, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new IdentityTokenCleanupResult(refreshDeleted, emailVerificationDeleted, passwordResetDeleted);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<int> DeleteExpiredAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        DateTimeOffset expiredBefore,
        CancellationToken cancellationToken)
    {
        var sql = $"DELETE FROM {tableName} WHERE expires_at <= @expired_before";
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("@expired_before", expiredBefore);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record IdentityTokenCleanupResult(
    int RefreshTokensDeleted,
    int EmailVerificationTokensDeleted,
    int PasswordResetTokensDeleted);
