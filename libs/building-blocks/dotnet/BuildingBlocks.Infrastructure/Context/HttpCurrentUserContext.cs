using System.Security.Claims;
using BuildingBlocks.Application.Context;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.Context;

public sealed class HttpCurrentUserContext : ICurrentUserContext, BuildingBlocks.Application.Interfaces.ICurrentUserContext
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => Guid.TryParse(FirstClaimValue("sub", "userId"), out var userId) ? userId : null;

    public string? Email => FirstClaimValue(ClaimTypes.Email, "email");

    public IReadOnlyCollection<string> Roles => ClaimValues(ClaimTypes.Role, "role", "roles");

    public IReadOnlyCollection<string> Permissions => ClaimValues("permission", "permissions");

    private string? FirstClaimValue(params string[] claimTypes)
    {
        return Claims(claimTypes).Select(claim => claim.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private IReadOnlyCollection<string> ClaimValues(params string[] claimTypes)
    {
        return Claims(claimTypes)
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IEnumerable<Claim> Claims(params string[] claimTypes)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        return user.Claims.Where(claim => claimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase));
    }
}
