namespace BuildingBlocks.Application.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
}
