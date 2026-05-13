using BuildingBlocks.Application.Context;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.Context;

public sealed class HttpCorrelationContext : ICorrelationContext, BuildingBlocks.Application.Interfaces.ICorrelationContext
{
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpCorrelationContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public string? CorrelationId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers[CorrelationHeaderName].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
