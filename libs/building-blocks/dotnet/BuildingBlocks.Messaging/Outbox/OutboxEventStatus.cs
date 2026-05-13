namespace BuildingBlocks.Messaging.Outbox;

public enum OutboxEventStatus
{
    Pending,
    Processing,
    Published,
    DeadLetter
}

public static class OutboxEventStatusValues
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Published = "published";
    public const string DeadLetter = "dead_letter";

    public static string ToStorageValue(this OutboxEventStatus status) => status switch
    {
        OutboxEventStatus.Pending => Pending,
        OutboxEventStatus.Processing => Processing,
        OutboxEventStatus.Published => Published,
        OutboxEventStatus.DeadLetter => DeadLetter,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown outbox event status.")
    };

    public static OutboxEventStatus FromStorageValue(string value) => value switch
    {
        Pending => OutboxEventStatus.Pending,
        Processing => OutboxEventStatus.Processing,
        Published => OutboxEventStatus.Published,
        DeadLetter => OutboxEventStatus.DeadLetter,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown outbox event status value.")
    };
}
