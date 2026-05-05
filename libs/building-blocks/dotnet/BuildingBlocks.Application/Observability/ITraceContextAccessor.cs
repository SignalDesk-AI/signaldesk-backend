namespace BuildingBlocks.Application.Observability;

public interface ITraceContextAccessor
{
    string? TraceId { get; }

    string? SpanId { get; }
}
