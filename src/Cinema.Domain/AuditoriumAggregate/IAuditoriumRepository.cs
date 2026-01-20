using Cinema.Domain.AuditoriumAggregate.ValueObjects;

namespace Cinema.Domain.AuditoriumAggregate;

public interface IAuditoriumRepository
{
    Task<Auditorium?> GetByIdAsync(AuditoriumId id, CancellationToken ct = default);
    Task<IEnumerable<Auditorium>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Auditorium auditorium, CancellationToken ct = default);
}
