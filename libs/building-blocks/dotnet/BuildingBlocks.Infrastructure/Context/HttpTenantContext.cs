using BuildingBlocks.Application.Context;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.Context;

public sealed class HttpTenantContext : ITenantContext, BuildingBlocks.Application.Interfaces.ITenantContext
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers[TenantHeaderName].FirstOrDefault();
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }
}
