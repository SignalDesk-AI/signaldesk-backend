namespace SignalDesk.Identity.Application;

public sealed record IdentityFlowOptions
{
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);

    public TimeSpan EmailVerificationTokenLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan PasswordResetTokenLifetime { get; init; } = TimeSpan.FromHours(1);

    public bool ReturnPasswordResetTokenToCaller { get; init; }
}
