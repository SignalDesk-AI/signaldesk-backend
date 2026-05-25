namespace SignalDesk.Identity.Domain;

public sealed class EmailVerificationToken
{
    private EmailVerificationToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public TokenLifecycleState GetState(DateTimeOffset now)
    {
        if (ConsumedAt.HasValue)
        {
            return TokenLifecycleState.Consumed;
        }

        return ExpiresAt <= now
            ? TokenLifecycleState.Expired
            : TokenLifecycleState.Active;
    }

    public bool CanBeConsumed(DateTimeOffset now)
    {
        return GetState(now) == TokenLifecycleState.Active;
    }

    public static EmailVerificationToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        Guid? tokenId = null)
    {
        EnsureGuid(userId, nameof(userId));
        EnsureRequired(tokenHash, nameof(tokenHash));

        if (expiresAt <= now)
        {
            throw new IdentityDomainException("Email verification token expiry must be in the future.");
        }

        return new EmailVerificationToken(
            tokenId ?? Guid.NewGuid(),
            userId,
            tokenHash,
            expiresAt,
            consumedAt: null,
            createdAt: now);
    }

    public void Consume(DateTimeOffset now)
    {
        var state = GetState(now);
        if (state != TokenLifecycleState.Active)
        {
            throw new IdentityDomainException($"Email verification token cannot be consumed because it is {state.ToString().ToLowerInvariant()}.");
        }

        ConsumedAt = now;
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
