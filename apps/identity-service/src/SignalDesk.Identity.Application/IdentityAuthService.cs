using BuildingBlocks.Application.Context;
using SignalDesk.Identity.Domain;

namespace SignalDesk.Identity.Application;

public sealed class IdentityAuthService : IIdentityAuthService
{
    private readonly IIdentityRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenHashService _tokenHashService;
    private readonly IIdentityTokenGenerator _tokenGenerator;
    private readonly IIdentityClock _clock;
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly IIdentityOutboxWriter _outboxWriter;
    private readonly IWorkspaceMembershipReader _workspaceMembershipReader;
    private readonly IRefreshTokenRotator _refreshTokenRotator;
    private readonly ICorrelationContext _correlationContext;
    private readonly IdentityFlowOptions _options;

    public IdentityAuthService(
        IIdentityRepository repository,
        IPasswordHasher passwordHasher,
        ITokenHashService tokenHashService,
        IIdentityTokenGenerator tokenGenerator,
        IIdentityClock clock,
        IIdentityUnitOfWork unitOfWork,
        IIdentityOutboxWriter outboxWriter,
        IWorkspaceMembershipReader workspaceMembershipReader,
        IRefreshTokenRotator refreshTokenRotator,
        ICorrelationContext correlationContext,
        IdentityFlowOptions? options = null)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenHashService = tokenHashService;
        _tokenGenerator = tokenGenerator;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _outboxWriter = outboxWriter;
        _workspaceMembershipReader = workspaceMembershipReader;
        _refreshTokenRotator = refreshTokenRotator;
        _correlationContext = correlationContext;
        _options = options ?? new IdentityFlowOptions();
    }

    public async Task<IdentityResult<RegisterUserResult>> RegisterAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return IdentityResult<RegisterUserResult>.Failure(IdentityApplicationError.Validation("Email is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return IdentityResult<RegisterUserResult>.Failure(IdentityApplicationError.Validation("Password is required."));
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            return IdentityResult<RegisterUserResult>.Failure(IdentityApplicationError.Validation("Display name is required."));
        }

        var normalizedEmail = NormalizeEmail(command.Email);
        var existingUser = await _repository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            return IdentityResult<RegisterUserResult>.Failure(IdentityApplicationError.Conflict(
                IdentityApplicationErrorCodes.EmailAlreadyRegistered,
                "Email is already registered."));
        }

        var result = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var user = UserAccount.Register(
                normalizedEmail,
                _passwordHasher.HashPassword(command.Password),
                command.DisplayName,
                now,
                command.AvatarUrl,
                correlationId: _correlationContext.CorrelationId);

            var emailVerificationSecret = _tokenGenerator.GenerateEmailVerificationToken(
                now,
                now.Add(_options.EmailVerificationTokenLifetime));
            var emailVerificationToken = EmailVerificationToken.Issue(
                user.Id,
                emailVerificationSecret.Hash,
                emailVerificationSecret.ExpiresAt,
                now);

            var tokenPairResult = await IssueTokenPairAsync(user, command.TenantId, command.DeviceId, now, ct);
            if (tokenPairResult.IsFailure)
            {
                return IdentityResult<RegisterUserResult>.Failure(tokenPairResult.Error!);
            }

            var tokenPair = tokenPairResult.Value!;

            await _repository.AddUserAsync(user, ct);
            await _repository.AddEmailVerificationTokenAsync(emailVerificationToken, ct);
            await _repository.AddRefreshTokenAsync(tokenPair.RefreshTokenEntity, ct);

            await AddDomainEventsToOutboxAsync(user, ct);
            user.ClearDomainEvents();

            return IdentityResult<RegisterUserResult>.Success(new RegisterUserResult(
                user.Id,
                user.Email,
                user.EmailVerified,
                emailVerificationSecret.Value,
                emailVerificationSecret.ExpiresAt,
                tokenPair.Result));
        }, cancellationToken);

        return result;
    }

    public async Task<IdentityResult<LoginResult>> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return IdentityResult<LoginResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidCredentials,
                "Email or password is invalid."));
        }

        var normalizedEmail = NormalizeEmail(command.Email);
        var user = await _repository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !_passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
        {
            return IdentityResult<LoginResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidCredentials,
                "Email or password is invalid."));
        }

        if (!user.CanAuthenticate)
        {
            return IdentityResult<LoginResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.UserDisabled,
                "User account cannot authenticate."));
        }

        var result = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            user.MarkLoginSucceeded(now);
            var tokenPairResult = await IssueTokenPairAsync(user, command.TenantId, command.DeviceId, now, ct);
            if (tokenPairResult.IsFailure)
            {
                return IdentityResult<LoginResult>.Failure(tokenPairResult.Error!);
            }

            var tokenPair = tokenPairResult.Value!;
            await _repository.AddRefreshTokenAsync(tokenPair.RefreshTokenEntity, ct);

            return IdentityResult<LoginResult>.Success(new LoginResult(tokenPair.Result));
        }, cancellationToken);

        return result;
    }

    public async Task<IdentityResult<RefreshTokenResult>> RefreshAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return RefreshFailure(RefreshTokenOutcome.Invalid);
        }

        var tokenHash = _tokenHashService.HashToken(command.RefreshToken);

        // Pre-check: load token for tenant-mismatch and membership checks.
        // The actual rotation uses an atomic consume-and-replace operation.
        var existingToken = await _repository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (existingToken is null)
        {
            return RefreshFailure(RefreshTokenOutcome.Invalid);
        }

        var now = _clock.UtcNow;
        var tokenState = existingToken.GetState(now);
        if (tokenState == TokenLifecycleState.Expired)
        {
            return RefreshFailure(RefreshTokenOutcome.Expired);
        }

        // Do NOT pre-check Revoked here. The atomic rotator distinguishes
        // explicitly-revoked (TokenRevoked → Revoked) from rotated-consumed
        // (TokenAlreadyConsumed → Replayed) by checking for a replacement token.

        if (command.TenantId.HasValue && existingToken.TenantId != command.TenantId)
        {
            return RefreshFailure(RefreshTokenOutcome.TenantMismatch);
        }

        var user = await _repository.GetUserByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null || !user.CanAuthenticate)
        {
            return RefreshFailure(RefreshTokenOutcome.Invalid);
        }

        if (existingToken.TenantId.HasValue)
        {
            var membership = await _workspaceMembershipReader.GetMembershipAsync(
                existingToken.UserId,
                existingToken.TenantId.Value,
                _correlationContext.CorrelationId,
                cancellationToken);
            if (!membership.IsActive)
            {
                // Revoke the presented token immediately per v6.0 source-of-truth:
                // inactive/revoked/suspended/missing membership must revoke the token.
                await _unitOfWork.ExecuteInTransactionAsync(ct =>
                {
                    existingToken.Revoke(_clock.UtcNow);
                    return Task.CompletedTask;
                }, cancellationToken);

                return RefreshFailure(RefreshTokenOutcome.TenantMembershipRevoked);
            }
        }

        // Atomic rotation: consume the presented token and persist the replacement
        // in a single compare-and-swap operation. This prevents concurrent refresh
        // requests from both succeeding with the same token.
        var rotationNow = _clock.UtcNow;
        var newRefreshTokenExpiresAt = rotationNow.Add(_options.RefreshTokenLifetime);
        var newRefreshTokenSecret = _tokenGenerator.GenerateRefreshToken(rotationNow, newRefreshTokenExpiresAt);

        var rotationResult = await _refreshTokenRotator.TryRotateAsync(
            tokenHash,
            newRefreshTokenSecret.Hash,
            newRefreshTokenExpiresAt,
            rotationNow,
            command.DeviceId,
            cancellationToken);

        switch (rotationResult.Outcome)
        {
            case RefreshTokenRotationOutcome.TokenNotFound:
                return RefreshFailure(RefreshTokenOutcome.Invalid);

            case RefreshTokenRotationOutcome.TokenExpired:
                return RefreshFailure(RefreshTokenOutcome.Expired);

            case RefreshTokenRotationOutcome.TokenRevoked:
                return RefreshFailure(RefreshTokenOutcome.Revoked);

            case RefreshTokenRotationOutcome.TokenAlreadyConsumed:
                return RefreshFailure(RefreshTokenOutcome.Replayed);

            case RefreshTokenRotationOutcome.Success:
                break;

            default:
                return RefreshFailure(RefreshTokenOutcome.Invalid);
        }

        var rotatedToken = rotationResult.RotatedToken!;
        var accessTokenExpiresAt = rotationNow.Add(_options.AccessTokenLifetime);
        var accessToken = _tokenGenerator.GenerateAccessToken(
            user, existingToken.TenantId, rotationNow, accessTokenExpiresAt);

        var result = new RefreshTokenResult(
            RefreshTokenOutcome.Valid,
            new TokenPairResult(
                accessToken.Value,
                accessToken.ExpiresAt,
                newRefreshTokenSecret.Value,
                rotatedToken.ExpiresAt,
                user.Id,
                user.Email,
                user.EmailVerified,
                rotatedToken.TenantId));

        return IdentityResult<RefreshTokenResult>.Success(result);
    }

    public Task<IdentityResult<LogoutResult>> LogoutAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        return RevokeRefreshTokenAsync(new RevokeRefreshTokenCommand(command.RefreshToken), cancellationToken);
    }

    public async Task<IdentityResult<LogoutResult>> RevokeRefreshTokenAsync(
        RevokeRefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return IdentityResult<LogoutResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidRefreshToken,
                "Refresh token is invalid."));
        }

        var tokenHash = _tokenHashService.HashToken(command.RefreshToken);
        var refreshToken = await _repository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (refreshToken is null)
        {
            return IdentityResult<LogoutResult>.Success(new LogoutResult(false));
        }

        await _unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            refreshToken.Revoke(_clock.UtcNow);
            return Task.CompletedTask;
        }, cancellationToken);

        return IdentityResult<LogoutResult>.Success(new LogoutResult(true));
    }

    public async Task<IdentityResult<VerifyEmailResult>> VerifyEmailAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.VerificationToken))
        {
            return IdentityResult<VerifyEmailResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidEmailVerificationToken,
                "Email verification token is invalid."));
        }

        var tokenHash = _tokenHashService.HashToken(command.VerificationToken);
        var token = await _repository.GetEmailVerificationTokenByHashAsync(tokenHash, cancellationToken);
        if (token is null)
        {
            return IdentityResult<VerifyEmailResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidEmailVerificationToken,
                "Email verification token is invalid."));
        }

        var now = _clock.UtcNow;
        var tokenState = token.GetState(now);
        if (tokenState == TokenLifecycleState.Expired)
        {
            return IdentityResult<VerifyEmailResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.EmailVerificationTokenExpired,
                "Email verification token is expired."));
        }

        if (tokenState == TokenLifecycleState.Consumed)
        {
            return IdentityResult<VerifyEmailResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.EmailVerificationTokenConsumed,
                "Email verification token has already been used."));
        }

        var user = await _repository.GetUserByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return IdentityResult<VerifyEmailResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidEmailVerificationToken,
                "Email verification token is invalid."));
        }

        var result = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var verifiedAt = _clock.UtcNow;
            token.Consume(verifiedAt);
            user.VerifyEmail(verifiedAt, _correlationContext.CorrelationId);
            await AddDomainEventsToOutboxAsync(user, ct);
            user.ClearDomainEvents();

            return new VerifyEmailResult(user.Id, user.Email, verifiedAt);
        }, cancellationToken);

        return IdentityResult<VerifyEmailResult>.Success(result);
    }

    public async Task<IdentityResult<RequestPasswordResetResult>> RequestPasswordResetAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return IdentityResult<RequestPasswordResetResult>.Failure(IdentityApplicationError.Validation("Email is required."));
        }

        var normalizedEmail = NormalizeEmail(command.Email);
        var user = await _repository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.CanAuthenticate)
        {
            return IdentityResult<RequestPasswordResetResult>.Success(new RequestPasswordResetResult(true, null, null));
        }

        var result = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.UtcNow;
            var resetSecret = _tokenGenerator.GeneratePasswordResetToken(now, now.Add(_options.PasswordResetTokenLifetime));
            var resetToken = PasswordResetToken.Issue(user.Id, resetSecret.Hash, resetSecret.ExpiresAt, now);
            await _repository.AddPasswordResetTokenAsync(resetToken, ct);

            return new RequestPasswordResetResult(
                true,
                _options.ReturnPasswordResetTokenToCaller ? resetSecret.Value : null,
                _options.ReturnPasswordResetTokenToCaller ? resetSecret.ExpiresAt : null);
        }, cancellationToken);

        return IdentityResult<RequestPasswordResetResult>.Success(result);
    }

    public async Task<IdentityResult<ConfirmPasswordResetResult>> ConfirmPasswordResetAsync(
        ConfirmPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ResetToken))
        {
            return IdentityResult<ConfirmPasswordResetResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidPasswordResetToken,
                "Password reset token is invalid."));
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return IdentityResult<ConfirmPasswordResetResult>.Failure(IdentityApplicationError.Validation("New password is required."));
        }

        var tokenHash = _tokenHashService.HashToken(command.ResetToken);
        var token = await _repository.GetPasswordResetTokenByHashAsync(tokenHash, cancellationToken);
        if (token is null)
        {
            return IdentityResult<ConfirmPasswordResetResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidPasswordResetToken,
                "Password reset token is invalid."));
        }

        var now = _clock.UtcNow;
        var tokenState = token.GetState(now);
        if (tokenState == TokenLifecycleState.Expired)
        {
            return IdentityResult<ConfirmPasswordResetResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.PasswordResetTokenExpired,
                "Password reset token is expired."));
        }

        if (tokenState == TokenLifecycleState.Consumed)
        {
            return IdentityResult<ConfirmPasswordResetResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.PasswordResetTokenConsumed,
                "Password reset token has already been used."));
        }

        var user = await _repository.GetUserByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.CanAuthenticate)
        {
            return IdentityResult<ConfirmPasswordResetResult>.Failure(IdentityApplicationError.Unauthenticated(
                IdentityApplicationErrorCodes.InvalidPasswordResetToken,
                "Password reset token is invalid."));
        }

        var result = await _unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            var changedAt = _clock.UtcNow;
            token.Consume(changedAt);
            user.ChangePasswordHash(_passwordHasher.HashPassword(command.NewPassword), changedAt);

            return Task.FromResult(new ConfirmPasswordResetResult(user.Id, user.Email, changedAt));
        }, cancellationToken);

        return IdentityResult<ConfirmPasswordResetResult>.Success(result);
    }

    private async Task<IdentityResult<IssuedTokenPair>> IssueTokenPairAsync(
        UserAccount user,
        Guid? tenantId,
        string? deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (tenantId.HasValue)
        {
            var membership = await _workspaceMembershipReader.GetMembershipAsync(
                user.Id,
                tenantId.Value,
                _correlationContext.CorrelationId,
                cancellationToken);
            if (!membership.IsActive)
            {
                return IdentityResult<IssuedTokenPair>.Failure(IdentityApplicationError.Forbidden(
                    IdentityApplicationErrorCodes.TenantMembershipRevoked,
                    "Tenant membership is inactive or revoked."));
            }
        }

        var accessTokenExpiresAt = now.Add(_options.AccessTokenLifetime);
        var refreshTokenExpiresAt = now.Add(_options.RefreshTokenLifetime);
        var accessToken = _tokenGenerator.GenerateAccessToken(user, tenantId, now, accessTokenExpiresAt);
        var refreshTokenSecret = _tokenGenerator.GenerateRefreshToken(now, refreshTokenExpiresAt);
        var refreshToken = RefreshToken.Issue(
            user.Id,
            refreshTokenSecret.Hash,
            refreshTokenSecret.ExpiresAt,
            now,
            tenantId,
            deviceId);

        var result = new TokenPairResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshTokenSecret.Value,
            refreshToken.ExpiresAt,
            user.Id,
            user.Email,
            user.EmailVerified,
            tenantId);

        return IdentityResult<IssuedTokenPair>.Success(new IssuedTokenPair(result, refreshToken));
    }

    private async Task AddDomainEventsToOutboxAsync(UserAccount user, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in user.DomainEvents)
        {
            switch (domainEvent)
            {
                case UserRegisteredDomainEvent userRegistered:
                    await _outboxWriter.AddAsync(IdentityOutboxEnvelopeFactory.UserRegistered(userRegistered), cancellationToken);
                    break;
                case UserEmailVerifiedDomainEvent userEmailVerified:
                    await _outboxWriter.AddAsync(IdentityOutboxEnvelopeFactory.UserEmailVerified(userEmailVerified), cancellationToken);
                    break;
            }
        }
    }

    private static IdentityResult<RefreshTokenResult> RefreshFailure(RefreshTokenOutcome outcome)
    {
        return IdentityResult<RefreshTokenResult>.Success(new RefreshTokenResult(outcome));
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private sealed record IssuedTokenPair(TokenPairResult Result, RefreshToken RefreshTokenEntity);
}
