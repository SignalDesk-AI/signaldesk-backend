using System.Text.Json;
using BuildingBlocks.Messaging.Outbox;
using Npgsql;
using SignalDesk.Identity.Application;
using SignalDesk.Identity.Domain;

namespace SignalDesk.Identity.Infrastructure.Persistence;

// Single scoped service implementing IIdentityUnitOfWork, IIdentityRepository,
// IIdentityOutboxWriter, and IRefreshTokenRotator. All share one NpgsqlConnection.
//
// Change tracking: entities loaded via Get*Async are tracked. When
// ExecuteInTransactionAsync commits, tracked entities are flushed — UPDATE
// SQL is emitted for any entity whose mutable fields may have been changed
// by application code (e.g. refreshToken.Revoke(), user.VerifyEmail(),
// token.Consume(), user.ChangePasswordHash()).
//
// This solves the impedance mismatch: IdentityAuthService mutates domain
// objects in memory; the infrastructure persists those mutations on commit.
public sealed class TransactionBoundIdentityService :
    IIdentityUnitOfWork,
    IIdentityRepository,
    IIdentityOutboxWriter,
    IRefreshTokenRotator,
    IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private bool _disposed;

    // Change tracking — entities loaded from the database whose in-memory
    // state may be mutated by application code before commit.
    private readonly Dictionary<Guid, UserAccount> _trackedUsers = new();
    private readonly Dictionary<Guid, RefreshToken> _trackedRefreshTokens = new();
    private readonly Dictionary<Guid, EmailVerificationToken> _trackedEmailVerificationTokens = new();
    private readonly Dictionary<Guid, PasswordResetToken> _trackedPasswordResetTokens = new();

    public TransactionBoundIdentityService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct)
    {
        _connection ??= await _connectionFactory.CreateConnectionAsync(ct);
        return _connection;
    }

    private NpgsqlTransaction? Transaction => _transaction;

    // ────────────────────────────────────────────────────────────
    // IIdentityUnitOfWork — with change tracking flush
    // ────────────────────────────────────────────────────────────

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        _transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await operation(cancellationToken);
            await FlushTrackedChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        _transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await FlushTrackedChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ────────────────────────────────────────────────────────────
    // IIdentityRepository
    // ────────────────────────────────────────────────────────────

    public async Task<UserAccount?> GetUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, email, password_hash, display_name, avatar_url, status, email_verified,
                   last_login_at, created_at, updated_at
            FROM identity.users WHERE LOWER(email) = LOWER(@email)
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@email", normalizedEmail);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var user = MapUser(reader);
        _trackedUsers[user.Id] = user;
        return user;
    }

    public async Task<UserAccount?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, email, password_hash, display_name, avatar_url, status, email_verified,
                   last_login_at, created_at, updated_at
            FROM identity.users WHERE id = @id
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var user = MapUser(reader);
        _trackedUsers[user.Id] = user;
        return user;
    }

    public async Task AddUserAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO identity.users
                (id, email, password_hash, display_name, avatar_url, status, email_verified, last_login_at, created_at, updated_at)
            VALUES (@id, @email, @password_hash, @display_name, @avatar_url, @status, @email_verified, @last_login_at, @created_at, @updated_at)
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@password_hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@display_name", user.DisplayName);
        cmd.Parameters.AddWithValue("@avatar_url", (object?)user.AvatarUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", user.Status.ToString());
        cmd.Parameters.AddWithValue("@email_verified", user.EmailVerified);
        cmd.Parameters.AddWithValue("@last_login_at", (object?)user.LastLoginAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", user.CreatedAt);
        cmd.Parameters.AddWithValue("@updated_at", user.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _trackedUsers[user.Id] = user;
    }

    public async Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, user_id, tenant_id, token_hash, device_id, expires_at, revoked_at, replaced_by_token_id, created_at
            FROM identity.refresh_tokens WHERE token_hash = @token_hash
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@token_hash", tokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var token = MapRefreshToken(reader);
        _trackedRefreshTokens[token.Id] = token;
        return token;
    }

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO identity.refresh_tokens
                (id, user_id, tenant_id, token_hash, device_id, expires_at, revoked_at, created_at)
            VALUES (@id, @user_id, @tenant_id, @token_hash, @device_id, @expires_at, @revoked_at, @created_at)
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@id", refreshToken.Id);
        cmd.Parameters.AddWithValue("@user_id", refreshToken.UserId);
        cmd.Parameters.AddWithValue("@tenant_id", (object?)refreshToken.TenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@token_hash", refreshToken.TokenHash);
        cmd.Parameters.AddWithValue("@device_id", (object?)refreshToken.DeviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@expires_at", refreshToken.ExpiresAt);
        cmd.Parameters.AddWithValue("@revoked_at", (object?)refreshToken.RevokedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", refreshToken.CreatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _trackedRefreshTokens[refreshToken.Id] = refreshToken;
    }

    public async Task<EmailVerificationToken?> GetEmailVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, user_id, token_hash, expires_at, consumed_at, created_at
            FROM identity.email_verification_tokens WHERE token_hash = @token_hash
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@token_hash", tokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var token = MapEmailVerificationToken(reader);
        _trackedEmailVerificationTokens[token.Id] = token;
        return token;
    }

    public async Task AddEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO identity.email_verification_tokens
                (id, user_id, token_hash, expires_at, consumed_at, created_at)
            VALUES (@id, @user_id, @token_hash, @expires_at, @consumed_at, @created_at)
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@id", token.Id);
        cmd.Parameters.AddWithValue("@user_id", token.UserId);
        cmd.Parameters.AddWithValue("@token_hash", token.TokenHash);
        cmd.Parameters.AddWithValue("@expires_at", token.ExpiresAt);
        cmd.Parameters.AddWithValue("@consumed_at", (object?)token.ConsumedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", token.CreatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _trackedEmailVerificationTokens[token.Id] = token;
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, user_id, token_hash, expires_at, consumed_at, created_at
            FROM identity.password_reset_tokens WHERE token_hash = @token_hash
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@token_hash", tokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var token = MapPasswordResetToken(reader);
        _trackedPasswordResetTokens[token.Id] = token;
        return token;
    }

    public async Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO identity.password_reset_tokens
                (id, user_id, token_hash, expires_at, consumed_at, created_at)
            VALUES (@id, @user_id, @token_hash, @expires_at, @consumed_at, @created_at)
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@id", token.Id);
        cmd.Parameters.AddWithValue("@user_id", token.UserId);
        cmd.Parameters.AddWithValue("@token_hash", token.TokenHash);
        cmd.Parameters.AddWithValue("@expires_at", token.ExpiresAt);
        cmd.Parameters.AddWithValue("@consumed_at", (object?)token.ConsumedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", token.CreatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _trackedPasswordResetTokens[token.Id] = token;
    }

    public async Task RevokeActiveRefreshTokensAsync(Guid userId, Guid? tenantId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        var sql = tenantId.HasValue
            ? """
              UPDATE identity.refresh_tokens SET revoked_at = @revoked_at
              WHERE user_id = @user_id AND tenant_id = @tenant_id
                AND revoked_at IS NULL AND expires_at > @revoked_at
              """
            : """
              UPDATE identity.refresh_tokens SET revoked_at = @revoked_at
              WHERE user_id = @user_id AND tenant_id IS NULL
                AND revoked_at IS NULL AND expires_at > @revoked_at
              """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@user_id", userId);
        cmd.Parameters.AddWithValue("@revoked_at", revokedAt);
        if (tenantId.HasValue) cmd.Parameters.AddWithValue("@tenant_id", tenantId.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredEmailVerificationTokensAsync(DateTimeOffset expiredBefore, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM identity.email_verification_tokens WHERE expires_at <= @expired_before";
        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@expired_before", expiredBefore);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredPasswordResetTokensAsync(DateTimeOffset expiredBefore, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM identity.password_reset_tokens WHERE expires_at <= @expired_before";
        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@expired_before", expiredBefore);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredRefreshTokensAsync(DateTimeOffset expiredBefore, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM identity.refresh_tokens WHERE expires_at <= @expired_before";
        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@expired_before", expiredBefore);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    // ────────────────────────────────────────────────────────────
    // IIdentityOutboxWriter
    // ────────────────────────────────────────────────────────────

    public async Task AddAsync(OutboxEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ops.outbox_events (
                id, service_name, event_type, event_version, exchange, routing_key,
                tenant_id, aggregate_type, aggregate_id, aggregate_version,
                correlation_id, causation_id, actor_id,
                payload, headers, status, retry_count, max_retries, next_retry_at, created_at
            ) VALUES (
                @id, @service_name, @event_type, @event_version, @exchange, @routing_key,
                @tenant_id, @aggregate_type, @aggregate_id, @aggregate_version,
                @correlation_id, @causation_id, @actor_id,
                @payload, @headers, @status, @retry_count, @max_retries, @next_retry_at, @created_at
            )
            """;

        var conn = await GetConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@service_name", "identity-service");
        cmd.Parameters.AddWithValue("@event_type", envelope.EventType);
        cmd.Parameters.AddWithValue("@event_version", envelope.EventVersion);
        cmd.Parameters.AddWithValue("@exchange", envelope.Exchange);
        cmd.Parameters.AddWithValue("@routing_key", envelope.RoutingKey);
        cmd.Parameters.AddWithValue("@tenant_id", (object?)envelope.TenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aggregate_type", (object?)envelope.AggregateType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aggregate_id", (object?)envelope.AggregateId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aggregate_version", (object?)envelope.AggregateVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@correlation_id", (object?)envelope.CorrelationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@causation_id", (object?)envelope.CausationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@actor_id", (object?)envelope.ActorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@payload", envelope.Payload);
        cmd.Parameters.AddWithValue("@headers", string.IsNullOrEmpty(envelope.Headers) ? "{}" : envelope.Headers);
        cmd.Parameters.AddWithValue("@status", OutboxEventStatusValues.Pending);
        cmd.Parameters.AddWithValue("@retry_count", 0);
        cmd.Parameters.AddWithValue("@max_retries", envelope.MaxRetries);
        cmd.Parameters.AddWithValue("@next_retry_at", DateTimeOffset.UtcNow);
        cmd.Parameters.AddWithValue("@created_at", DateTimeOffset.UtcNow);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    // ────────────────────────────────────────────────────────────
    // IRefreshTokenRotator
    // ────────────────────────────────────────────────────────────

    public async Task<RefreshTokenRotationResult> TryRotateAsync(
        string presentedTokenHash,
        string newTokenHash,
        DateTimeOffset newExpiresAt,
        DateTimeOffset now,
        string? newDeviceId = null,
        CancellationToken cancellationToken = default)
    {
        var conn = await GetConnectionAsync(cancellationToken);

        var existingToken = await GetRefreshTokenForUpdateAsync(conn, presentedTokenHash, cancellationToken);
        if (existingToken is null)
        {
            return new RefreshTokenRotationResult(RefreshTokenRotationOutcome.TokenNotFound);
        }

        var state = existingToken.GetState(now);
        if (state == TokenLifecycleState.Expired)
        {
            return new RefreshTokenRotationResult(RefreshTokenRotationOutcome.TokenExpired);
        }

        if (state == TokenLifecycleState.Revoked)
        {
            // Distinguish explicit revocation from replayed (consumed-by-rotation).
            // A revoked token with replaced_by_token_id was consumed during a prior
            // rotation — that's a replay. A revoked token without a replacement was
            // explicitly revoked (e.g., by logout or admin action).
            var hasReplacement = await HasReplacementTokenAsync(conn, existingToken.Id, cancellationToken);
            var outcome = hasReplacement
                ? RefreshTokenRotationOutcome.TokenAlreadyConsumed
                : RefreshTokenRotationOutcome.TokenRevoked;
            return new RefreshTokenRotationResult(outcome);
        }

        var replacement = RefreshToken.Issue(
            existingToken.UserId, newTokenHash, newExpiresAt, now,
            existingToken.TenantId, newDeviceId ?? existingToken.DeviceId);

        var rotatedToken = await RotateRefreshTokenCteAsync(conn, presentedTokenHash, replacement, now, cancellationToken);
        if (rotatedToken is null)
        {
            return new RefreshTokenRotationResult(RefreshTokenRotationOutcome.TokenAlreadyConsumed);
        }

        return new RefreshTokenRotationResult(
            RefreshTokenRotationOutcome.Success,
            RotatedToken: rotatedToken,
            PreviousToken: existingToken);
    }

    // ────────────────────────────────────────────────────────────
    // IAsyncDisposable
    // ────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_transaction is not null) await _transaction.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    // ────────────────────────────────────────────────────────────
    // Change tracking flush
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Flushes all tracked entity mutations to the database. Called automatically
    /// before commit in ExecuteInTransactionAsync. This handles in-memory mutations
    /// like RefreshToken.Revoke(), EmailVerificationToken.Consume(),
    /// UserAccount.VerifyEmail(), UserAccount.ChangePasswordHash(), and
    /// UserAccount.MarkLoginSucceeded() that the application performs on loaded entities.
    /// </summary>
    private async Task FlushTrackedChangesAsync(CancellationToken cancellationToken)
    {
        if (_connection is null || _transaction is null) return;

        foreach (var user in _trackedUsers.Values)
        {
            await FlushUserAsync(user, cancellationToken);
        }

        foreach (var token in _trackedRefreshTokens.Values)
        {
            await FlushRefreshTokenAsync(token, cancellationToken);
        }

        foreach (var token in _trackedEmailVerificationTokens.Values)
        {
            await FlushEmailVerificationTokenAsync(token, cancellationToken);
        }

        foreach (var token in _trackedPasswordResetTokens.Values)
        {
            await FlushPasswordResetTokenAsync(token, cancellationToken);
        }

        _trackedUsers.Clear();
        _trackedRefreshTokens.Clear();
        _trackedEmailVerificationTokens.Clear();
        _trackedPasswordResetTokens.Clear();
    }

    private async Task FlushUserAsync(UserAccount user, CancellationToken ct)
    {
        // Persist all mutable fields. On INSERT (first flush), this is a no-op
        // because AddUserAsync already inserted. On subsequent flushes within the
        // same transaction scope, this captures mutations like VerifyEmail(),
        // ChangePasswordHash(), MarkLoginSucceeded().
        const string sql = """
            UPDATE identity.users SET
                password_hash = @password_hash,
                status = @status,
                email_verified = @email_verified,
                last_login_at = @last_login_at,
                updated_at = @updated_at
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection, _transaction);
        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@password_hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@status", user.Status.ToString());
        cmd.Parameters.AddWithValue("@email_verified", user.EmailVerified);
        cmd.Parameters.AddWithValue("@last_login_at", (object?)user.LastLoginAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated_at", user.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task FlushRefreshTokenAsync(RefreshToken token, CancellationToken ct)
    {
        // Persist revoked_at mutation (from Revoke() or Rotate()).
        const string sql = """
            UPDATE identity.refresh_tokens SET revoked_at = @revoked_at
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection, _transaction);
        cmd.Parameters.AddWithValue("@id", token.Id);
        cmd.Parameters.AddWithValue("@revoked_at", (object?)token.RevokedAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task FlushEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken ct)
    {
        // Persist consumed_at mutation (from Consume()).
        const string sql = """
            UPDATE identity.email_verification_tokens SET consumed_at = @consumed_at
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection, _transaction);
        cmd.Parameters.AddWithValue("@id", token.Id);
        cmd.Parameters.AddWithValue("@consumed_at", (object?)token.ConsumedAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task FlushPasswordResetTokenAsync(PasswordResetToken token, CancellationToken ct)
    {
        // Persist consumed_at mutation (from Consume()).
        const string sql = """
            UPDATE identity.password_reset_tokens SET consumed_at = @consumed_at
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection, _transaction);
        cmd.Parameters.AddWithValue("@id", token.Id);
        cmd.Parameters.AddWithValue("@consumed_at", (object?)token.ConsumedAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ────────────────────────────────────────────────────────────
    // Private helpers
    // ────────────────────────────────────────────────────────────

    private async Task<bool> HasReplacementTokenAsync(
        NpgsqlConnection conn, Guid tokenId, CancellationToken ct)
    {
        const string sql = """
            SELECT replaced_by_token_id FROM identity.refresh_tokens WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@id", tokenId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not DBNull && result is not null;
    }

    private async Task<RefreshToken?> GetRefreshTokenForUpdateAsync(
        NpgsqlConnection conn, string tokenHash, CancellationToken ct)
    {
        const string sql = """
            SELECT id, user_id, tenant_id, token_hash, device_id, expires_at, revoked_at, replaced_by_token_id, created_at
            FROM identity.refresh_tokens WHERE token_hash = @token_hash FOR UPDATE
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@token_hash", tokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRefreshToken(reader) : null;
    }

    private async Task<RefreshToken?> RotateRefreshTokenCteAsync(
        NpgsqlConnection conn, string presentedHash, RefreshToken replacement, DateTimeOffset now, CancellationToken ct)
    {
        const string sql = """
            WITH revoked AS (
                UPDATE identity.refresh_tokens SET revoked_at = @now
                WHERE token_hash = @presented_hash AND revoked_at IS NULL AND expires_at > @now
                RETURNING id, user_id, tenant_id, token_hash, device_id, expires_at, revoked_at, replaced_by_token_id, created_at
            ),
            inserted AS (
                INSERT INTO identity.refresh_tokens (id, user_id, tenant_id, token_hash, device_id, expires_at, revoked_at, created_at)
                SELECT @new_id, r.user_id, r.tenant_id, @new_hash, @new_device_id, @new_expires_at, NULL, @now
                FROM revoked r
                RETURNING id, user_id, tenant_id, token_hash, device_id, expires_at, revoked_at, replaced_by_token_id, created_at
            ),
            linked AS (
                UPDATE identity.refresh_tokens t SET replaced_by_token_id = i.id
                FROM inserted i, revoked r
                WHERE t.id = r.id
                RETURNING t.id
            )
            SELECT id, user_id, tenant_id, token_hash, device_id, expires_at, revoked_at, replaced_by_token_id, created_at FROM inserted
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, Transaction);
        cmd.Parameters.AddWithValue("@presented_hash", presentedHash);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@new_id", replacement.Id);
        cmd.Parameters.AddWithValue("@new_hash", replacement.TokenHash);
        cmd.Parameters.AddWithValue("@new_device_id", (object?)replacement.DeviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@new_expires_at", replacement.ExpiresAt);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRefreshToken(reader) : null;
    }

    // ──── Entity mapping ────

    private static UserAccount MapUser(NpgsqlDataReader r)
    {
        var id = r.GetGuid(0);
        var email = r.GetString(1);
        var passwordHash = r.GetString(2);
        var displayName = r.GetString(3);
        var avatarUrl = r.IsDBNull(4) ? null : r.GetString(4);
        var statusString = r.GetString(5);
        var status = Enum.TryParse<UserAccountStatus>(statusString, ignoreCase: true, out var parsed)
            ? parsed
            : UserAccountStatus.Pending;
        var emailVerified = r.GetBoolean(6);
        var lastLoginAt = r.IsDBNull(7) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(7);
        var createdAt = r.GetFieldValue<DateTimeOffset>(8);
        var updatedAt = r.GetFieldValue<DateTimeOffset>(9);

        return UserAccount.Rehydrate(
            id, email, passwordHash, displayName, avatarUrl,
            status, emailVerified, lastLoginAt, createdAt, updatedAt);
    }

    private static RefreshToken MapRefreshToken(NpgsqlDataReader r)
    {
        var id = r.GetGuid(0);
        var userId = r.GetGuid(1);
        var tenantId = r.IsDBNull(2) ? (Guid?)null : r.GetGuid(2);
        var tokenHash = r.GetString(3);
        var deviceId = r.IsDBNull(4) ? null : r.GetString(4);
        var expiresAt = r.GetFieldValue<DateTimeOffset>(5);
        var revokedAt = r.IsDBNull(6) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(6);
        var createdAt = r.GetFieldValue<DateTimeOffset>(8);

        var token = RefreshToken.Issue(userId, tokenHash, expiresAt, createdAt, tenantId, deviceId, id);
        if (revokedAt.HasValue) token.Revoke(revokedAt.Value);
        return token;
    }

    private static EmailVerificationToken MapEmailVerificationToken(NpgsqlDataReader r)
    {
        var id = r.GetGuid(0);
        var userId = r.GetGuid(1);
        var tokenHash = r.GetString(2);
        var expiresAt = r.GetFieldValue<DateTimeOffset>(3);
        var consumedAt = r.IsDBNull(4) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(4);
        var createdAt = r.GetFieldValue<DateTimeOffset>(5);

        var token = EmailVerificationToken.Issue(userId, tokenHash, expiresAt, createdAt, id);
        if (consumedAt.HasValue) token.Consume(consumedAt.Value);
        return token;
    }

    private static PasswordResetToken MapPasswordResetToken(NpgsqlDataReader r)
    {
        var id = r.GetGuid(0);
        var userId = r.GetGuid(1);
        var tokenHash = r.GetString(2);
        var expiresAt = r.GetFieldValue<DateTimeOffset>(3);
        var consumedAt = r.IsDBNull(4) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(4);
        var createdAt = r.GetFieldValue<DateTimeOffset>(5);

        var token = PasswordResetToken.Issue(userId, tokenHash, expiresAt, createdAt, id);
        if (consumedAt.HasValue) token.Consume(consumedAt.Value);
        return token;
    }
}
