namespace SignalDesk.Identity.Application;

public static class IdentityEnvelopeErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string InternalError = "INTERNAL_ERROR";
}

public static class IdentityApplicationErrorCodes
{
    public const string ValidationFailed = "IDENTITY_VALIDATION_FAILED";
    public const string EmailAlreadyRegistered = "IDENTITY_EMAIL_ALREADY_REGISTERED";
    public const string InvalidCredentials = "IDENTITY_INVALID_CREDENTIALS";
    public const string UserDisabled = "IDENTITY_USER_DISABLED";
    public const string InvalidRefreshToken = "IDENTITY_REFRESH_TOKEN_INVALID";
    public const string RefreshTokenExpired = "IDENTITY_REFRESH_TOKEN_EXPIRED";
    public const string RefreshTokenRevoked = "IDENTITY_REFRESH_TOKEN_REVOKED";
    public const string RefreshTokenReplayed = "IDENTITY_REFRESH_TOKEN_REPLAYED";
    public const string TenantMismatch = "IDENTITY_REFRESH_TENANT_MISMATCH";
    public const string TenantMembershipRevoked = "IDENTITY_TENANT_MEMBERSHIP_REVOKED";
    public const string InvalidEmailVerificationToken = "IDENTITY_EMAIL_VERIFICATION_TOKEN_INVALID";
    public const string EmailVerificationTokenExpired = "IDENTITY_EMAIL_VERIFICATION_TOKEN_EXPIRED";
    public const string EmailVerificationTokenConsumed = "IDENTITY_EMAIL_VERIFICATION_TOKEN_CONSUMED";
    public const string InvalidPasswordResetToken = "IDENTITY_PASSWORD_RESET_TOKEN_INVALID";
    public const string PasswordResetTokenExpired = "IDENTITY_PASSWORD_RESET_TOKEN_EXPIRED";
    public const string PasswordResetTokenConsumed = "IDENTITY_PASSWORD_RESET_TOKEN_CONSUMED";
}

public sealed record IdentityApplicationError(
    string Code,
    string Message,
    string EnvelopeCode)
{
    public static IdentityApplicationError Validation(string message)
    {
        return new IdentityApplicationError(
            IdentityApplicationErrorCodes.ValidationFailed,
            message,
            IdentityEnvelopeErrorCodes.ValidationError);
    }

    public static IdentityApplicationError Conflict(string code, string message)
    {
        return new IdentityApplicationError(code, message, IdentityEnvelopeErrorCodes.Conflict);
    }

    public static IdentityApplicationError Unauthenticated(string code, string message)
    {
        return new IdentityApplicationError(code, message, IdentityEnvelopeErrorCodes.Unauthenticated);
    }

    public static IdentityApplicationError Forbidden(string code, string message)
    {
        return new IdentityApplicationError(code, message, IdentityEnvelopeErrorCodes.Forbidden);
    }

    public static IdentityApplicationError NotFound(string code, string message)
    {
        return new IdentityApplicationError(code, message, IdentityEnvelopeErrorCodes.NotFound);
    }
}

public sealed class IdentityResult
{
    private IdentityResult(bool isSuccess, IdentityApplicationError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IdentityApplicationError? Error { get; }

    public static IdentityResult Success()
    {
        return new IdentityResult(true, null);
    }

    public static IdentityResult Failure(IdentityApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new IdentityResult(false, error);
    }
}

public sealed class IdentityResult<T>
{
    private IdentityResult(bool isSuccess, T? value, IdentityApplicationError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public IdentityApplicationError? Error { get; }

    public static IdentityResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new IdentityResult<T>(true, value, null);
    }

    public static IdentityResult<T> Failure(IdentityApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new IdentityResult<T>(false, default, error);
    }
}
