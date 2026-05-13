namespace BuildingBlocks.Application.Context;

public interface ICurrentUserContext
{
    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> Permissions { get; }

    bool IsAuthenticated => UserId.HasValue;
}
