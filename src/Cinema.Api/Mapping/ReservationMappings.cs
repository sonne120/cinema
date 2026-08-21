using Cinema.Contracts.Reservations;
using Cinema.Domain.ReservationAggregate;

namespace Cinema.Api.Mapping;

public static class ReservationMappings
{
    public static ReservationResponse ToResponse(this Reservation reservation) =>
        new(reservation.Id.Value,
            reservation.ShowtimeId.Value,
            reservation.Seats.Select(s => new SeatResponse(s.Row, s.Number)).ToList(),
            reservation.Status.Value.ToString(),
            reservation.CreatedAt,
            reservation.ExpiresAt,
            reservation.ConfirmedAt);
}
