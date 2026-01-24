namespace Cinema.Domain.UserAggregate.ValueObjects;

public sealed class UserId : IEquatable<UserId>
{
    public Guid Value { get; }

    private UserId(Guid value)
    {
        Value = value;
    }

    public static UserId CreateUnique() => new(Guid.NewGuid());

    public static UserId Create(Guid value) => new(value);

    public bool Equals(UserId? other)
    {
        return other is not null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is UserId other && Equals(other);
    }

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();
}
