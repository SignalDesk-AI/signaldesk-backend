namespace BuildingBlocks.Application.Interfaces;

public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
