using Cinema.Domain.Common.Models;
using Cinema.Domain.PaymentAggregate.Events;
using Cinema.Domain.PaymentAggregate.ValueObjects;

namespace Cinema.Domain.PaymentAggregate;

public sealed class Payment : AggregateRoot<PaymentId>
{
    public Guid ReservationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Money Amount { get; private set; } = Money.Zero();
    public Money RefundedAmount { get; private set; } = Money.Zero();
    public PaymentStatus Status { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? TransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }

    private Payment() : base(PaymentId.CreateUnique()) { }

    private Payment(
        PaymentId id,
        Guid reservationId,
        Guid customerId,
        Money amount,
        PaymentMethod method) : base(id)
    {
        ReservationId = reservationId;
        CustomerId = customerId;
        Amount = amount;
        RefundedAmount = Money.Zero(amount.Currency);
        Status = PaymentStatus.Pending;
        Method = method;
        CreatedAt = DateTime.UtcNow;
    }

    public static Result<Payment> Create(
        Guid reservationId,
        Guid customerId,
        Money amount,
        PaymentMethod method)
    {
        if (reservationId == Guid.Empty)
            return Result.Failure<Payment>("Reservation ID is required");

        if (amount.Amount <= 0)
            return Result.Failure<Payment>("Amount must be positive");

        var paymentId = PaymentId.CreateUnique();
        var payment = new Payment(
            paymentId,
            reservationId,
            customerId,
            amount,
            method);

        payment.RaiseDomainEvent(new PaymentCreatedEvent(
            paymentId,
            reservationId,
            customerId,
            amount.Amount,
            amount.Currency,
            method));

        return Result.Success(payment);
    }

    public Result StartProcessing()
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure("Payment can only start processing from Pending state");

        Status = PaymentStatus.Processing;
        RaiseDomainEvent(new PaymentProcessingStartedEvent(Id));
        return Result.Success();
    }

    public Result Complete(string transactionId)
    {
        if (Status != PaymentStatus.Processing)
            return Result.Failure("Payment can only be completed from Processing state");

        if (string.IsNullOrWhiteSpace(transactionId))
            return Result.Failure("Transaction ID is required");

        Status = PaymentStatus.Completed;
        TransactionId = transactionId;
        ProcessedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PaymentCompletedEvent(Id, transactionId, Amount.Amount));
        return Result.Success();
    }

    public Result Decline(string reason)
    {
        if (Status != PaymentStatus.Processing)
            return Result.Failure("Payment can only be declined from Processing state");

        Status = PaymentStatus.Declined;
        FailureReason = reason;
        ProcessedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PaymentDeclinedEvent(Id, reason));
        return Result.Success();
    }

    public Result Fail(string reason)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Processing)
            return Result.Failure("Payment can only fail from Pending or Processing state");

        Status = PaymentStatus.Failed;
        FailureReason = reason;

        RaiseDomainEvent(new PaymentFailedEvent(Id, reason));
        return Result.Success();
    }

    public Result Refund(string reason)
    {
        if (!CanBeRefunded())
            return Result.Failure("Payment cannot be refunded");

        var refundAmount = GetRefundableAmount();
        RefundedAmount = refundAmount;
        Status = PaymentStatus.Refunded;
        RefundedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PaymentRefundedEvent(Id, refundAmount.Amount, reason));
        return Result.Success();
    }

    public Result PartialRefund(Money amount, string reason)
    {
        if (!CanBeRefunded())
            return Result.Failure("Payment cannot be refunded");

        if (amount.Amount > GetRefundableAmount().Amount)
            return Result.Failure("Refund amount exceeds refundable amount");

        RefundedAmount = RefundedAmount.Add(amount);
        Status = RefundedAmount.Amount >= Amount.Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;
        RefundedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PaymentPartiallyRefundedEvent(Id, amount.Amount, reason));
        return Result.Success();
    }

    public bool CanBeRefunded()
    {
        return Status == PaymentStatus.Completed || Status == PaymentStatus.PartiallyRefunded;
    }

    public Money GetRefundableAmount()
    {
        return Amount.Subtract(RefundedAmount);
    }
}
