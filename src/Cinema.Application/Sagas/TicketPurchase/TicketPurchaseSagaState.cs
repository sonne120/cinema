using Cinema.Domain.PaymentAggregate.ValueObjects;
using Cinema.Domain.ReservationAggregate.ValueObjects;

namespace Cinema.Application.Sagas.TicketPurchase;

public class TicketPurchaseSagaState : SagaState
{
    public override string SagaType => "TicketPurchase";
    public override int TotalSteps => 4;
    public override TimeSpan Timeout => TimeSpan.FromMinutes(10);

    public Guid ShowtimeId { get; set; }
    public Guid CustomerId { get; set; }
    public List<SeatNumber> Seats { get; set; } = new();
    public PaymentMethod PaymentMethod { get; set; }
    public string? CardNumber { get; set; }
    public string? CardHolderName { get; set; }

    public Guid? ReservationId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? TicketId { get; set; }
    public string? TicketNumber { get; set; }

    public decimal TotalPrice { get; set; }
    public decimal AmountCharged { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime ScreeningTime { get; set; }
    public string AuditoriumName { get; set; } = string.Empty;
    public string? TransactionId { get; set; }

    public bool SeatsReserved { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool TicketIssued { get; set; }
    public bool ReservationConfirmed { get; set; }

    public List<SagaStepLog> StepLogs { get; set; } = new();

    public void LogStep(string stepName, bool success, string message)
    {
        StepLogs.Add(new SagaStepLog
        {
            StepName = stepName,
            Success = success,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class SagaStepLog
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
public record PurchaseTicketCommand(
    Guid ShowtimeId,
    Guid CustomerId,
    List<SeatNumber> Seats,
    PaymentMethod PaymentMethod,
    string? CardNumber = null,
    string? CardHolderName = null);
public record PurchaseTicketResult(
    Guid TicketId,
    string TicketNumber,
    Guid ReservationId,
    Guid PaymentId,
    string MovieTitle,
    DateTime ScreeningTime,
    List<SeatNumber> Seats,
    decimal TotalPrice);
