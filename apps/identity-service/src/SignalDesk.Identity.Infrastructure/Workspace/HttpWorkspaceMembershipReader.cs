using System.Net.Http.Json;
using System.Text.Json;
using SignalDesk.Identity.Application;

namespace SignalDesk.Identity.Infrastructure.Workspace;

// Real HTTP client for workspace membership lookup. Calls the workspace-service
// internal endpoint: GET /internal/memberships?userId={userId}&tenantId={tenantId}
// Expected Day 5 response shape:
// { "data": { "userId": "uuid", "tenantId": "uuid", "status": "Active" }, "meta": { "correlationId": "..." } }
//
// Fails closed: returns NotFound on errors so tenant-scoped refresh is blocked
// when the workspace lookup cannot be trusted.
public sealed class HttpWorkspaceMembershipReader : IWorkspaceMembershipReader
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public HttpWorkspaceMembershipReader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WorkspaceMembershipLookupResult> GetMembershipAsync(
        Guid userId,
        Guid tenantId,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/internal/memberships?userId={userId}&tenantId={tenantId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return WorkspaceMembershipLookupResult.Missing(userId, tenantId);
            }

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<MembershipResponse>(_jsonOptions, cancellationToken);
            if (body?.Data is null)
            {
                return WorkspaceMembershipLookupResult.Missing(userId, tenantId);
            }

            return body.Data.Status switch
            {
                "Active" => WorkspaceMembershipLookupResult.Active(userId, tenantId),
                "Revoked" => WorkspaceMembershipLookupResult.Revoked(userId, tenantId),
                "Suspended" => new WorkspaceMembershipLookupResult(userId, tenantId, WorkspaceMembershipStatus.Suspended),
                _ => WorkspaceMembershipLookupResult.Missing(userId, tenantId)
            };
        }
        catch (HttpRequestException)
        {
            // Fail closed: when workspace-service is unavailable, return NotFound
            // so tenant-scoped refresh is blocked. This prevents issuing tokens
            // based on untrusted membership state.
            return WorkspaceMembershipLookupResult.Missing(userId, tenantId);
        }
    }

    private sealed record MembershipResponse(MembershipData? Data);

    private sealed record MembershipData(Guid UserId, Guid TenantId, string Status);
}
