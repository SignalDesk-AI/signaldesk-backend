namespace BuildingBlocks.Application.Context;

public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
