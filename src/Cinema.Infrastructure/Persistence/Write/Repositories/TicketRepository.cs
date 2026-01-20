using Cinema.Domain.TicketAggregate;
using Cinema.Domain.TicketAggregate.ValueObjects;
using Cinema.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence.Write.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly CinemaDbContext _context;
    
    public TicketRepository(CinemaDbContext context) => _context = context;

    public async Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
        => await _context.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Ticket?> GetByTicketNumberAsync(string ticketNumber, CancellationToken ct = default)
        => await _context.Tickets.FirstOrDefaultAsync(x => x.TicketNumber == ticketNumber, ct);

    public async Task<Ticket?> GetByReservationIdAsync(Guid reservationId, CancellationToken ct = default)
        => await _context.Tickets.FirstOrDefaultAsync(x => x.ReservationId == reservationId, ct);

    public async Task<IEnumerable<Ticket>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
        => await _context.Tickets.Where(x => x.CustomerId == customerId).ToListAsync(ct);

    public async Task<IEnumerable<Ticket>> GetByShowtimeIdAsync(Guid showtimeId, CancellationToken ct = default)
        => await _context.Tickets.Where(x => x.ShowtimeId == showtimeId).ToListAsync(ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        await _context.Tickets.AddAsync(ticket, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync(ct);
    }
}
