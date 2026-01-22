using Cinema.Domain.Common.Models;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Cinema.Domain.TicketAggregate.ValueObjects;

namespace Cinema.Domain.TicketAggregate.Events;

public record TicketIssuedEvent(
    TicketId TicketId,
    string TicketNumber,
    Guid ReservationId,
    Guid PaymentId,
    Guid ShowtimeId,
    Guid CustomerId,
    string MovieTitle,
    DateTime ScreeningTime,
    IReadOnlyCollection<SeatNumber> Seats) : DomainEvent;

public record TicketUsedEvent(
    TicketId TicketId,
    string TicketNumber,
    DateTime UsedAt) : DomainEvent;

public record TicketCancelledEvent(
    TicketId TicketId,
    string TicketNumber) : DomainEvent;

public record TicketRefundedEvent(
    TicketId TicketId,
    string TicketNumber) : DomainEvent;
