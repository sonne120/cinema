using Cinema.Application.Common.Interfaces.Persistence;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Cinema.Application.Sagas.TicketPurchase.Steps;


public class ConfirmReservationStep : ISagaStep<TicketPurchaseSagaState>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IShowtimeRepository _showtimeRepository;
    private readonly ILogger<ConfirmReservationStep> _logger;

    public string StepName => "ConfirmReservation";
    public int StepOrder => 3;

    public ConfirmReservationStep(
        IReservationRepository reservationRepository,
        IShowtimeRepository showtimeRepository,
        ILogger<ConfirmReservationStep> logger)
    {
        _reservationRepository = reservationRepository;
        _showtimeRepository = showtimeRepository;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: Executing {StepName}", state.SagaId, StepName);

        try
        {
            if (!state.ReservationId.HasValue)
                return StepResult.Failure("Reservation ID is required");

            if (!state.PaymentId.HasValue)
                return StepResult.Failure("Payment ID is required");

            var reservationId = ReservationId.Create(state.ReservationId.Value);
            var reservation = await _reservationRepository.GetByIdAsync(reservationId, ct);
            if (reservation == null)
                return StepResult.Failure("Reservation not found");

            if (!reservation.Status.IsPending)
                return StepResult.Failure("Reservation cannot be confirmed");

            // Confirm reservation
            reservation.Confirm();
            _reservationRepository.Update(reservation);

            // Update state
            state.ReservationConfirmed = true;
            state.LogStep(StepName, true, "Reservation confirmed");

            _logger.LogInformation("Saga {SagaId}: {StepName} completed", state.SagaId, StepName);

            return StepResult.Success("Reservation confirmed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId}: {StepName} failed", state.SagaId, StepName);
            state.LogStep(StepName, false, ex.Message);
            return StepResult.Failure(ex.Message);
        }
    }

    public Task<StepResult> CompensateAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: Compensating {StepName}", state.SagaId, StepName);

        // Compensation is handled by ReserveSeatsStep (releasing seats)
        // and ProcessPaymentStep (refunding payment)
        state.ReservationConfirmed = false;

        return Task.FromResult(StepResult.Success("Confirmation reverted"));
    }

    public bool ShouldCompensate(TicketPurchaseSagaState state)
    {
        return state.ReservationConfirmed;
    }
}
