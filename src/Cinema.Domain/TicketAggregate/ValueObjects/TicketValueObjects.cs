namespace Cinema.Domain.TicketAggregate.ValueObjects;


public readonly record struct TicketId(Guid Value)
{
    public static TicketId CreateUnique() => new(Guid.NewGuid());
    public static TicketId Create(Guid value) => new(value);
    
    public static implicit operator Guid(TicketId id) => id.Value;
}

public enum TicketStatus
{
    Issued,
    Used,
    Cancelled,
    Refunded
}
