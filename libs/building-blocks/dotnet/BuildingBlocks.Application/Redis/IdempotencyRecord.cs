namespace BuildingBlocks.Application.Redis;

public sealed record IdempotencyRecord(
    Guid TenantId,
    string Scope,
    string Key,
    string RequestHash,
    IdempotencyStatus Status,
    int? ResponseCode,
    string? ResponseBody,
    DateTimeOffset ExpiresAt);

public enum IdempotencyStatus
{
    Processing,
    Completed,
    Failed
}
