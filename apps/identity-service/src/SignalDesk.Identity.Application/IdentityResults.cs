namespace SignalDesk.Identity.Application;

public sealed record TokenPairResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    bool EmailVerified,
    Guid? TenantId);

public sealed record RegisterUserResult(
    Guid UserId,
    string Email,
    bool EmailVerified,
    string EmailVerificationToken,
    DateTimeOffset EmailVerificationTokenExpiresAt,
    TokenPairResult Tokens);

public sealed record LoginResult(TokenPairResult Tokens);

public enum RefreshTokenOutcome
{
    Valid = 0,
    Invalid = 1,
    Expired = 2,
    Revoked = 3,
    Replayed = 4,
    TenantMembershipRevoked = 5,
    TenantMismatch = 6
}

public sealed record RefreshTokenResult(
    RefreshTokenOutcome Outcome,
    TokenPairResult? Tokens = null);

public sealed record LogoutResult(bool Revoked);

public sealed record VerifyEmailResult(
    Guid UserId,
    string Email,
    DateTimeOffset VerifiedAt);

public sealed record RequestPasswordResetResult(
    bool Accepted,
    string? PasswordResetToken,
    DateTimeOffset? PasswordResetTokenExpiresAt);

public sealed record ConfirmPasswordResetResult(
    Guid UserId,
    string Email,
    DateTimeOffset PasswordChangedAt);
