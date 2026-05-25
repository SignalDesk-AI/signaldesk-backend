using SignalDesk.Identity.Application;

namespace SignalDesk.Identity.Infrastructure.Security;

public sealed class SystemUtcClock : IIdentityClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
