using Cinema.Domain.PaymentAggregate;
using Cinema.Domain.PaymentAggregate.ValueObjects;
using Cinema.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence.Write.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly CinemaDbContext _context;
    
    public PaymentRepository(CinemaDbContext context) => _context = context;

    public async Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken ct = default)
        => await _context.Payments.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Payment?> GetByReservationIdAsync(Guid reservationId, CancellationToken ct = default)
        => await _context.Payments.FirstOrDefaultAsync(x => x.ReservationId == reservationId, ct);

    public async Task<IEnumerable<Payment>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
        => await _context.Payments.Where(x => x.CustomerId == customerId).ToListAsync(ct);

    public async Task<IEnumerable<Payment>> GetPendingPaymentsAsync(CancellationToken ct = default)
        => await _context.Payments.Where(x => x.Status == PaymentStatus.Pending).ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _context.Payments.AddAsync(payment, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync(ct);
    }
}
