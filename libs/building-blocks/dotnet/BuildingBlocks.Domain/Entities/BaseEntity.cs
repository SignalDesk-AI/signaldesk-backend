namespace BuildingBlocks.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public Guid TenantId { get; protected set; }

    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; protected set; }

    public bool IsDeleted { get; protected set; }

    public DateTimeOffset? DeletedAt { get; protected set; }

    protected void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
    }

    protected void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    protected void MarkDeleted()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    protected void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        Touch();
    }
}
