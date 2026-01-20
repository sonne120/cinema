using Cinema.Domain.Common.Models;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Cinema.Domain.TicketAggregate;
using Cinema.Domain.TicketAggregate.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Cinema.Application.Sagas.TicketPurchase.Steps;

public interface INotificationService
{
    Task SendTicketEmailAsync(Guid customerId, string ticketNumber, string movieTitle,
        DateTime screeningTime, string seats, string qrCode, CancellationToken ct);
    Task SendRefundNotificationAsync(Guid customerId, string ticketNumber,
        decimal refundAmount, string reason, CancellationToken ct);
}

public interface IAuditoriumService
{
    Task<string> GetAuditoriumNameAsync(Guid auditoriumId, CancellationToken ct);
}

public class IssueTicketStep : ISagaStep<TicketPurchaseSagaState>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IAuditoriumService _auditoriumService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<IssueTicketStep> _logger;

    public string StepName => "IssueTicket";
    public int StepOrder => 4;

    public IssueTicketStep(
        ITicketRepository ticketRepository, 
        IAuditoriumService auditoriumService,
        INotificationService notificationService, 
        ILogger<IssueTicketStep> logger)
    {
        _ticketRepository = ticketRepository;
        _auditoriumService = auditoriumService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: Executing {StepName}", state.SagaId, StepName);
        try
        {
            if (!state.ReservationId.HasValue || !state.PaymentId.HasValue || !state.ReservationConfirmed)
                return StepResult.Failure("Prerequisites not met");

            // Get auditorium name (defaulting if not available)
            state.AuditoriumName = await _auditoriumService.GetAuditoriumNameAsync(Guid.Empty, ct);

            // Convert seats to ticket seat format
            var ticketSeats = state.Seats
                .Select(s => Cinema.Domain.ReservationAggregate.ValueObjects.SeatNumber.Create(s.Row, s.Number))
                .ToList();

            var ticketResult = Ticket.Create(
                state.ReservationId.Value, 
                state.PaymentId.Value,
                state.ShowtimeId, 
                state.CustomerId, 
                state.MovieTitle, 
                state.ScreeningTime,
                state.AuditoriumName, 
                ticketSeats, 
                Money.Create(state.TotalPrice));

            if (ticketResult.IsFailure) 
                return StepResult.Failure(ticketResult.Error);

            var ticket = ticketResult.Value;
            await _ticketRepository.AddAsync(ticket, ct);

            state.TicketId = ticket.Id.Value;
            state.TicketNumber = ticket.TicketNumber;
            state.TicketIssued = true;
            state.LogStep(StepName, true, $"Ticket issued: {ticket.TicketNumber}");

            return StepResult.Success($"Ticket issued: {ticket.TicketNumber}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId}: {StepName} failed", state.SagaId, StepName);
            return StepResult.Failure(ex.Message);
        }
    }

    public async Task<StepResult> CompensateAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: Compensating {StepName}", state.SagaId, StepName);
        try
        {
            if (!state.TicketId.HasValue) 
                return StepResult.Success("No ticket to cancel");

            var ticketId = TicketId.Create(state.TicketId.Value);
            var ticket = await _ticketRepository.GetByIdAsync(ticketId, ct);
            if (ticket != null)
            {
                ticket.Cancel();
                await _ticketRepository.UpdateAsync(ticket, ct);
            }
            state.TicketIssued = false;
            return StepResult.Success("Ticket cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId}: Compensation failed", state.SagaId);
            return StepResult.Failure(ex.Message);
        }
    }

    public bool ShouldCompensate(TicketPurchaseSagaState state) => state.TicketIssued && state.TicketId.HasValue;
}
