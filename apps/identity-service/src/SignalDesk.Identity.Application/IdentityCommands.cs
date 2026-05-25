namespace SignalDesk.Identity.Application;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    string? AvatarUrl = null,
    string? DeviceId = null,
    Guid? TenantId = null);

public sealed record LoginCommand(
    string Email,
    string Password,
    string? DeviceId = null,
    Guid? TenantId = null);

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? DeviceId = null,
    Guid? TenantId = null);

public sealed record LogoutCommand(
    string RefreshToken);

public sealed record RevokeRefreshTokenCommand(
    string RefreshToken);

public sealed record VerifyEmailCommand(
    string VerificationToken);

public sealed record RequestPasswordResetCommand(
    string Email);

public sealed record ConfirmPasswordResetCommand(
    string ResetToken,
    string NewPassword);
