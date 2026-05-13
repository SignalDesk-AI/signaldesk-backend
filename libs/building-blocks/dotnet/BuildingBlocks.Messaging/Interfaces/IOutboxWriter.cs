using BuildingBlocks.Messaging.Models;
using BuildingBlocks.Messaging.Outbox;

namespace BuildingBlocks.Messaging.Interfaces;

public interface IOutboxWriter
{
    Task AddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);

    Task AddAsync(OutboxEventEnvelope envelope, CancellationToken cancellationToken = default);
}
