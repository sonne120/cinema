using Cinema.Application.Common.Interfaces.Persistence;
using Cinema.Domain.Common.Models;
using Cinema.Domain.ReservationAggregate;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Cinema.Domain.ShowtimeAggregate.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Cinema.Application.Sagas.TicketPurchase.Steps;

public class ReserveSeatsStep : ISagaStep<TicketPurchaseSagaState>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IShowtimeRepository _showtimeRepository;
    private readonly ILogger<ReserveSeatsStep> _logger;

    public string StepName => "ReserveSeats";
    public int StepOrder => 1;

    public ReserveSeatsStep(
        IReservationRepository reservationRepository,
        IShowtimeRepository showtimeRepository,
        ILogger<ReserveSeatsStep> logger)
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
            var showtimeId = ShowtimeId.Create(state.ShowtimeId);
            var showtime = await _showtimeRepository.GetByIdAsync(showtimeId, ct);
            if (showtime == null)
                return StepResult.Failure($"Showtime {state.ShowtimeId} not found");

            var seatGuids = state.Seats.Select(s => GetSeatGuid(s, showtime.AuditoriumId)).ToList();
            if (!showtime.AreSeatsAvailable(seatGuids))
                return StepResult.Failure("One or more seats are not available");

            state.MovieTitle = showtime.MovieDetails.Title;
            state.ScreeningTime = showtime.ScreeningTime.Value;
            state.TotalPrice = 12.50m * state.Seats.Count;

            showtime.ReserveSeats(seatGuids);

            var reservationSeats = state.Seats
                .Select(s => Cinema.Domain.ReservationAggregate.ValueObjects.SeatNumber.Create(s.Row, s.Number))
                .ToList();
            
            var reservation = Reservation.Create(
                showtimeId,
                reservationSeats,
                DateTime.UtcNow);

            await _reservationRepository.AddAsync(reservation, ct);
            _showtimeRepository.Update(showtime);

            state.ReservationId = reservation.Id.Value;
            state.SeatsReserved = true;
            state.LogStep(StepName, true, $"Reserved {state.Seats.Count} seats");

            _logger.LogInformation("Saga {SagaId}: {StepName} completed - ReservationId: {ReservationId}",
                state.SagaId, StepName, reservation.Id.Value);

            return StepResult.Success($"Reserved {state.Seats.Count} seats");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId}: {StepName} failed", state.SagaId, StepName);
            state.LogStep(StepName, false, ex.Message);
            return StepResult.Failure(ex.Message);
        }
    }

    public async Task<StepResult> CompensateAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: Compensating {StepName}", state.SagaId, StepName);

        try
        {
            if (state.ReservationId.HasValue)
            {
                var reservationId = ReservationId.Create(state.ReservationId.Value);
                var reservation = await _reservationRepository.GetByIdAsync(reservationId, ct);
                if (reservation != null && reservation.Status.IsPending)
                {
                    reservation.Expire();
                    _reservationRepository.Update(reservation);
                }
            }

            var showtimeId = ShowtimeId.Create(state.ShowtimeId);
            var showtime = await _showtimeRepository.GetByIdAsync(showtimeId, ct);
            if (showtime != null)
            {
                _showtimeRepository.Update(showtime);
            }

            state.SeatsReserved = false;
            _logger.LogInformation("Saga {SagaId}: {StepName} compensated", state.SagaId, StepName);

            return StepResult.Success("Seats released");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId}: {StepName} compensation failed", state.SagaId, StepName);
            return StepResult.Failure(ex.Message);
        }
    }

    public bool ShouldCompensate(TicketPurchaseSagaState state)
    {
        return state.SeatsReserved && state.ReservationId.HasValue;
    }
    private static Guid GetSeatGuid(SeatNumber seat, Guid auditoriumId)
    {
        var bytes = auditoriumId.ToByteArray();
        bytes[14] = (byte)seat.Row;
        bytes[15] = (byte)seat.Number;
        return new Guid(bytes);
    }
}
