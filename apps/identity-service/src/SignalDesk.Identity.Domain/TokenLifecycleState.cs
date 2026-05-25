namespace SignalDesk.Identity.Domain;

public enum TokenLifecycleState
{
    Active = 0,
    Revoked = 1,
    Expired = 2,
    Consumed = 3
}
