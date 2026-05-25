using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using SignalDesk.Identity.Application;
using SignalDesk.Identity.Domain;

namespace SignalDesk.Identity.Infrastructure.Tokens;

// Token generator for Day 4. Secret tokens use cryptographically secure RNG + SHA256.
// Access tokens use RS256 JWT signing. Key material is loaded from environment variables;
// no secrets are committed to the repo.
public sealed class IdentityTokenGenerator : IIdentityTokenGenerator
{
    private readonly ITokenHashService _tokenHashService;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly RsaSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;

    public IdentityTokenGenerator(
        ITokenHashService tokenHashService,
        string issuer = "signaldesk-identity",
        string audience = "signaldesk-api")
    {
        _tokenHashService = tokenHashService;
        _issuer = issuer;
        _audience = audience;

        // Load RSA key from environment or generate ephemeral key for development.
        var rsa = RSA.Create(2048);
        var rsaKeyPem = Environment.GetEnvironmentVariable("IDENTITY_JWT_RSA_KEY");
        if (!string.IsNullOrEmpty(rsaKeyPem))
        {
            rsa.ImportFromPem(rsaKeyPem);
        }

        _signingKey = new RsaSecurityKey(rsa) { KeyId = "identity-rs256-001" };
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
    }

    public GeneratedAccessToken GenerateAccessToken(
        UserAccount user,
        Guid? tenantId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var jwtId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("email_verified", user.EmailVerified.ToString().ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Jti, jwtId.ToString()),
            new(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tid", tenantId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _signingCredentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

        return new GeneratedAccessToken(tokenValue, jwtId, issuedAt, expiresAt);
    }

    public GeneratedSecretToken GenerateRefreshToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        return GenerateSecretToken(issuedAt, expiresAt);
    }

    public GeneratedSecretToken GenerateEmailVerificationToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        return GenerateSecretToken(issuedAt, expiresAt);
    }

    public GeneratedSecretToken GeneratePasswordResetToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        return GenerateSecretToken(issuedAt, expiresAt);
    }

    private GeneratedSecretToken GenerateSecretToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        // 32 bytes of cryptographic randomness, Base64Url-encoded (43 chars).
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var value = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var hash = _tokenHashService.HashToken(value);

        return new GeneratedSecretToken(value, hash, issuedAt, expiresAt);
    }
}
