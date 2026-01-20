namespace Cinema.Domain.PaymentAggregate.ValueObjects;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId CreateUnique() => new(Guid.NewGuid());
    public static PaymentId Create(Guid value) => new(value);
    
    public static implicit operator Guid(PaymentId id) => id.Value;
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Declined,
    Refunded,
    PartiallyRefunded,
    Failed
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    PayPal,
    ApplePay,
    GooglePay,
    BankTransfer
}
