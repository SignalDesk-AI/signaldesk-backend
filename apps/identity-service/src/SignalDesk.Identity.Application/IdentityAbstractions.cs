using BuildingBlocks.Messaging.Outbox;
using SignalDesk.Identity.Domain;

namespace SignalDesk.Identity.Application;

public interface IIdentityClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}

public interface ITokenHashService
{
    string HashToken(string token);
}

public interface IIdentityTokenGenerator
{
    GeneratedAccessToken GenerateAccessToken(UserAccount user, Guid? tenantId, DateTimeOffset issuedAt, DateTimeOffset expiresAt);

    GeneratedSecretToken GenerateRefreshToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt);

    GeneratedSecretToken GenerateEmailVerificationToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt);

    GeneratedSecretToken GeneratePasswordResetToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt);
}

public sealed record GeneratedAccessToken(
    string Value,
    Guid JwtId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record GeneratedSecretToken(
    string Value,
    string Hash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public interface IRefreshTokenRotator
{
    Task<RefreshTokenRotationResult> TryRotateAsync(
        string presentedTokenHash,
        string newTokenHash,
        DateTimeOffset newExpiresAt,
        DateTimeOffset now,
        string? newDeviceId = null,
        CancellationToken cancellationToken = default);
}

public enum RefreshTokenRotationOutcome
{
    Success = 0,
    TokenNotFound = 1,
    TokenExpired = 2,
    TokenRevoked = 3,
    TokenAlreadyConsumed = 4
}

public sealed record RefreshTokenRotationResult(
    RefreshTokenRotationOutcome Outcome,
    RefreshToken? RotatedToken = null,
    RefreshToken? PreviousToken = null);

public interface IIdentityRepository
{
    Task<UserAccount?> GetUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddUserAsync(UserAccount user, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task<EmailVerificationToken?> GetEmailVerificationTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);

    Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    Task RevokeActiveRefreshTokensAsync(Guid userId, Guid? tenantId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredEmailVerificationTokensAsync(DateTimeOffset expiredBefore, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredPasswordResetTokensAsync(DateTimeOffset expiredBefore, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredRefreshTokensAsync(DateTimeOffset expiredBefore, CancellationToken cancellationToken = default);
}

public interface IIdentityUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}

public interface IIdentityOutboxWriter
{
    Task AddAsync(OutboxEventEnvelope envelope, CancellationToken cancellationToken = default);
}

public interface IWorkspaceMembershipReader
{
    Task<WorkspaceMembershipLookupResult> GetMembershipAsync(
        Guid userId,
        Guid tenantId,
        string? correlationId,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceMembershipLookupResult(
    Guid UserId,
    Guid TenantId,
    WorkspaceMembershipStatus Status)
{
    public bool IsActive => Status == WorkspaceMembershipStatus.Active;

    public static WorkspaceMembershipLookupResult Active(Guid userId, Guid tenantId)
    {
        return new WorkspaceMembershipLookupResult(userId, tenantId, WorkspaceMembershipStatus.Active);
    }

    public static WorkspaceMembershipLookupResult Revoked(Guid userId, Guid tenantId)
    {
        return new WorkspaceMembershipLookupResult(userId, tenantId, WorkspaceMembershipStatus.Revoked);
    }

    public static WorkspaceMembershipLookupResult Missing(Guid userId, Guid tenantId)
    {
        return new WorkspaceMembershipLookupResult(userId, tenantId, WorkspaceMembershipStatus.NotFound);
    }
}

public enum WorkspaceMembershipStatus
{
    Active = 0,
    Revoked = 1,
    Suspended = 2,
    NotFound = 3
}
