using Cinema.Domain.PaymentAggregate.ValueObjects;

namespace Cinema.Domain.PaymentAggregate;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken ct = default);
    Task<Payment?> GetByReservationIdAsync(Guid reservationId, CancellationToken ct = default);
    Task<IEnumerable<Payment>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task<IEnumerable<Payment>> GetPendingPaymentsAsync(CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}
