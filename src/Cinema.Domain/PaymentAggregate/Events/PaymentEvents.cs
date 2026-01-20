using Cinema.Domain.Common.Models;
using Cinema.Domain.PaymentAggregate.ValueObjects;

namespace Cinema.Domain.PaymentAggregate.Events;

public record PaymentCreatedEvent(
    PaymentId PaymentId,
    Guid ReservationId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentMethod Method) : DomainEvent;

public record PaymentProcessingStartedEvent(PaymentId PaymentId) : DomainEvent;
public record PaymentCompletedEvent(
    PaymentId PaymentId,
    string TransactionId,
    decimal Amount) : DomainEvent;

public record PaymentDeclinedEvent(
    PaymentId PaymentId,
    string Reason) : DomainEvent;

public record PaymentFailedEvent(
    PaymentId PaymentId,
    string Reason) : DomainEvent;

public record PaymentRefundedEvent(
    PaymentId PaymentId,
    decimal RefundAmount,
    string Reason) : DomainEvent;

public record PaymentPartiallyRefundedEvent(
    PaymentId PaymentId,
    decimal RefundAmount,
    string Reason) : DomainEvent;
