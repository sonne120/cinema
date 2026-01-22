using Cinema.Domain.Common.Models;

namespace Cinema.Application.Sagas.TicketPurchase.Events;
public record TicketPurchaseSagaStartedEvent(
    Guid SagaId,
    Guid ShowtimeId,
    Guid CustomerId,
    int SeatsCount) : DomainEvent;

public record TicketPurchaseSagaStepCompletedEvent(
    Guid SagaId,
    int StepNumber,
    string StepName,
    bool Success,
    string Message) : DomainEvent;

public record TicketPurchaseSagaCompletedEvent(
    Guid SagaId,
    Guid TicketId,
    string TicketNumber,
    Guid ReservationId,
    Guid PaymentId) : DomainEvent;

public record TicketPurchaseSagaFailedEvent(
    Guid SagaId,
    string Reason,
    int FailedAtStep) : DomainEvent;

public record TicketPurchaseSagaCompensationStartedEvent(
    Guid SagaId,
    string Reason) : DomainEvent;

public record TicketPurchaseSagaCompensatedEvent(
    Guid SagaId,
    bool PaymentRefunded,
    bool SeatsReleased,
    bool TicketCancelled) : DomainEvent;
