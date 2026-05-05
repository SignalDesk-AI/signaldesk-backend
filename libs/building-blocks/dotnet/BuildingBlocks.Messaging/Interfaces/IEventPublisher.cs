using BuildingBlocks.Messaging.Models;

namespace BuildingBlocks.Messaging.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
