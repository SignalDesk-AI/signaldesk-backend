using BuildingBlocks.Domain.Events;

namespace SignalDesk.Identity.Domain;

public sealed class UserAccount
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private UserAccount(
        Guid id,
        string email,
        string passwordHash,
        string displayName,
        string? avatarUrl,
        UserAccountStatus status,
        bool emailVerified,
        DateTimeOffset? lastLoginAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
        Status = status;
        EmailVerified = emailVerified;
        LastLoginAt = lastLoginAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string DisplayName { get; private set; }

    public string? AvatarUrl { get; private set; }

    public UserAccountStatus Status { get; private set; }

    public bool EmailVerified { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public bool CanAuthenticate => Status is UserAccountStatus.Pending or UserAccountStatus.Active;

    /// <summary>
    /// Rehydrate a persisted user from the database. Unlike Register, this preserves
    /// the stored status (including Disabled) and does not raise domain events.
    /// </summary>
    public static UserAccount Rehydrate(
        Guid id,
        string email,
        string passwordHash,
        string displayName,
        string? avatarUrl,
        UserAccountStatus status,
        bool emailVerified,
        DateTimeOffset? lastLoginAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new UserAccount(
            id,
            email,
            passwordHash,
            displayName,
            avatarUrl,
            status,
            emailVerified,
            lastLoginAt,
            createdAt,
            updatedAt);
    }

    public static UserAccount Register(
        string email,
        string passwordHash,
        string displayName,
        DateTimeOffset now,
        string? avatarUrl = null,
        Guid? userId = null,
        string? correlationId = null)
    {
        EnsureRequired(email, nameof(email));
        EnsureRequired(passwordHash, nameof(passwordHash));
        EnsureRequired(displayName, nameof(displayName));

        var user = new UserAccount(
            userId ?? Guid.NewGuid(),
            NormalizeEmail(email),
            passwordHash,
            displayName.Trim(),
            NormalizeOptional(avatarUrl),
            UserAccountStatus.Pending,
            emailVerified: false,
            lastLoginAt: null,
            createdAt: now,
            updatedAt: now);

        user.Raise(new UserRegisteredDomainEvent(
            Guid.NewGuid(),
            now,
            user.Id,
            user.Email,
            correlationId));

        return user;
    }

    public void MarkLoginSucceeded(DateTimeOffset now)
    {
        EnsureCanAuthenticate();

        LastLoginAt = now;
        UpdatedAt = now;
    }

    public void VerifyEmail(DateTimeOffset now, string? correlationId = null)
    {
        if (EmailVerified)
        {
            return;
        }

        EmailVerified = true;
        Status = UserAccountStatus.Active;
        UpdatedAt = now;

        Raise(new UserEmailVerifiedDomainEvent(
            Guid.NewGuid(),
            now,
            Id,
            Email,
            now,
            correlationId));
    }

    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    {
        EnsureRequired(passwordHash, nameof(passwordHash));
        EnsureCanAuthenticate();

        PasswordHash = passwordHash;
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        if (Status == UserAccountStatus.Disabled)
        {
            return;
        }

        Status = UserAccountStatus.Disabled;
        UpdatedAt = now;
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private void EnsureCanAuthenticate()
    {
        if (!CanAuthenticate)
        {
            throw new IdentityDomainException("User account cannot authenticate.");
        }
    }

    private void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
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
}
