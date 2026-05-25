using BuildingBlocks.Domain.Exceptions;

namespace SignalDesk.Identity.Domain;

public sealed class IdentityDomainException : DomainException
{
    public IdentityDomainException(string message)
        : base(message)
    {
    }
}
