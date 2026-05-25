namespace SignalDesk.Identity.Application;

public interface IIdentityAuthService
{
    Task<IdentityResult<RegisterUserResult>> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken = default);

    Task<IdentityResult<LoginResult>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);

    Task<IdentityResult<RefreshTokenResult>> RefreshAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default);

    Task<IdentityResult<LogoutResult>> LogoutAsync(LogoutCommand command, CancellationToken cancellationToken = default);

    Task<IdentityResult<LogoutResult>> RevokeRefreshTokenAsync(RevokeRefreshTokenCommand command, CancellationToken cancellationToken = default);

    Task<IdentityResult<VerifyEmailResult>> VerifyEmailAsync(VerifyEmailCommand command, CancellationToken cancellationToken = default);

    Task<IdentityResult<RequestPasswordResetResult>> RequestPasswordResetAsync(RequestPasswordResetCommand command, CancellationToken cancellationToken = default);

    Task<IdentityResult<ConfirmPasswordResetResult>> ConfirmPasswordResetAsync(ConfirmPasswordResetCommand command, CancellationToken cancellationToken = default);
}
