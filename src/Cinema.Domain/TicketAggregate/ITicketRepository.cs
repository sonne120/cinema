using Cinema.Domain.TicketAggregate.ValueObjects;

namespace Cinema.Domain.TicketAggregate;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default);
    Task<Ticket?> GetByTicketNumberAsync(string ticketNumber, CancellationToken ct = default);
    Task<Ticket?> GetByReservationIdAsync(Guid reservationId, CancellationToken ct = default);
    Task<IEnumerable<Ticket>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task<IEnumerable<Ticket>> GetByShowtimeIdAsync(Guid showtimeId, CancellationToken ct = default);
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
}
