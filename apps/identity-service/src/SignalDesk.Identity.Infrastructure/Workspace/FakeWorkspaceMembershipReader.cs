using SignalDesk.Identity.Application;

namespace SignalDesk.Identity.Infrastructure.Workspace;

// Fake workspace membership reader for testing and local development.
// Always returns Active for all lookups. Use HttpWorkspaceMembershipReader
// in production; keep this fake for unit tests and Day 4 local wiring.
public sealed class FakeWorkspaceMembershipReader : IWorkspaceMembershipReader
{
    public Task<WorkspaceMembershipLookupResult> GetMembershipAsync(
        Guid userId,
        Guid tenantId,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WorkspaceMembershipLookupResult.Active(userId, tenantId));
    }
}
