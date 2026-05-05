using System.Diagnostics;
using BuildingBlocks.Application.Observability;

namespace BuildingBlocks.Infrastructure.Observability;

public sealed class SystemDiagnosticsTraceContextAccessor : ITraceContextAccessor
{
    public string? TraceId => Activity.Current?.TraceId.ToString();

    public string? SpanId => Activity.Current?.SpanId.ToString();
}
