using BuildingBlocks.Application.Auditing;

namespace BuildingBlocks.Infrastructure.Auditing;

public sealed class NoopAuditLogWriter : IAuditLogWriter
{
    public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
