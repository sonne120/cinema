using Cinema.Domain.AuditoriumAggregate;
using Cinema.Domain.AuditoriumAggregate.ValueObjects;
using Cinema.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence.Write.Repositories;

public class AuditoriumRepository : IAuditoriumRepository
{
    private readonly CinemaDbContext _context;
    
    public AuditoriumRepository(CinemaDbContext context) => _context = context;

    public async Task<Auditorium?> GetByIdAsync(AuditoriumId id, CancellationToken ct = default)
        => await _context.Auditoriums.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IEnumerable<Auditorium>> GetAllAsync(CancellationToken ct = default)
        => await _context.Auditoriums.ToListAsync(ct);

    public async Task AddAsync(Auditorium auditorium, CancellationToken ct = default)
    {
        await _context.Auditoriums.AddAsync(auditorium, ct);
        await _context.SaveChangesAsync(ct);
    }
}
