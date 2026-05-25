namespace SignalDesk.Identity.Domain;

public sealed class RefreshToken
{
    private RefreshToken(
        Guid id,
        Guid userId,
        Guid? tenantId,
        string tokenHash,
        string? deviceId,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        TokenHash = tokenHash;
        DeviceId = deviceId;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string TokenHash { get; private set; }

    public string? DeviceId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsTenantScoped => TenantId.HasValue;

    public TokenLifecycleState GetState(DateTimeOffset now)
    {
        if (RevokedAt.HasValue)
        {
            return TokenLifecycleState.Revoked;
        }

        return ExpiresAt <= now
            ? TokenLifecycleState.Expired
            : TokenLifecycleState.Active;
    }

    public bool CanBeUsed(DateTimeOffset now)
    {
        return GetState(now) == TokenLifecycleState.Active;
    }

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        Guid? tenantId = null,
        string? deviceId = null,
        Guid? tokenId = null)
    {
        EnsureGuid(userId, nameof(userId));
        EnsureRequired(tokenHash, nameof(tokenHash));

        if (expiresAt <= now)
        {
            throw new IdentityDomainException("Refresh token expiry must be in the future.");
        }

        return new RefreshToken(
            tokenId ?? Guid.NewGuid(),
            userId,
            tenantId,
            tokenHash,
            NormalizeOptional(deviceId),
            expiresAt,
            revokedAt: null,
            createdAt: now);
    }

    public RefreshToken Rotate(
        string newTokenHash,
        DateTimeOffset newExpiresAt,
        DateTimeOffset now,
        string? newDeviceId = null,
        Guid? newTokenId = null)
    {
        EnsureCanBeUsed(now);
        EnsureRequired(newTokenHash, nameof(newTokenHash));

        if (newExpiresAt <= now)
        {
            throw new IdentityDomainException("Refresh token expiry must be in the future.");
        }

        Revoke(now);

        return Issue(
            UserId,
            newTokenHash,
            newExpiresAt,
            now,
            TenantId,
            newDeviceId ?? DeviceId,
            newTokenId);
    }

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt.HasValue)
        {
            return;
        }

        RevokedAt = now;
    }

    public void EnsureCanBeUsed(DateTimeOffset now)
    {
        var state = GetState(now);
        if (state != TokenLifecycleState.Active)
        {
            throw new IdentityDomainException($"Refresh token cannot be used because it is {state.ToString().ToLowerInvariant()}.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IdentityDomainException($"{parameterName} is required.");
        }
    }

    private static void EnsureGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new IdentityDomainException($"{parameterName} is required.");
        }
    }
}
