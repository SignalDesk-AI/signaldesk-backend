namespace BuildingBlocks.Application.Context;

public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant => TenantId.HasValue;

    Guid RequireTenantId()
    {
        return TenantId ?? throw new InvalidOperationException("Tenant context is required for this operation.");
    }
}
