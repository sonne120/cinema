namespace Cinema.Domain.AuditoriumAggregate.ValueObjects;

public readonly record struct AuditoriumId(Guid Value)
{
    public static AuditoriumId CreateUnique() => new(Guid.NewGuid());
    public static AuditoriumId Create(Guid value) => new(value);
    
    public static implicit operator Guid(AuditoriumId id) => id.Value;
}
