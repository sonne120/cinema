using Cinema.Application.Common.Interfaces.Persistence;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Cinema.Domain.ReservationAggregate;
using Cinema.Domain.ShowtimeAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence.Write.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly CinemaDbContext _dbContext;

    public ReservationRepository(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Reservation?> GetByIdAsync(ReservationId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<Reservation>> GetByShowtimeIdAsync(
        ShowtimeId showtimeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .Where(r => r.ShowtimeId == showtimeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Reservation>> GetExpiredReservationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbContext.Reservations
            .Where(r => r.Status.IsPending && r.ExpiresAt < now)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken = default)
    {
        await _dbContext.Reservations.AddAsync(reservation, cancellationToken);
    }

    public void Update(Reservation reservation)
    {
        _dbContext.Reservations.Update(reservation);
    }

    public void Remove(Reservation reservation)
    {
        _dbContext.Reservations.Remove(reservation);
    }
}
