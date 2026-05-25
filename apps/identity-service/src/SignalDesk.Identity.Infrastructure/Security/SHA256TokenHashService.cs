using System.Security.Cryptography;
using System.Text;
using SignalDesk.Identity.Application;

namespace SignalDesk.Identity.Infrastructure.Security;

// SHA256-based token hashing for refresh tokens, email verification tokens,
// and password reset tokens. Uses only .NET BCL (no external packages required).
// Tokens are stored only as hashes; the plaintext is returned to the caller once.
public sealed class SHA256TokenHashService : ITokenHashService
{
    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token is required.", nameof(token));
        }

        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
