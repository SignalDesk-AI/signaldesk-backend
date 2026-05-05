namespace BuildingBlocks.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
}
