using BuildingBlocks.Messaging.Models;

namespace BuildingBlocks.Messaging.Interfaces;

public interface IOutboxWriter
{
    Task AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
