using Cinema.Domain.AuditoriumAggregate;
using Cinema.Domain.PaymentAggregate;
using Cinema.Domain.ReservationAggregate;
using Cinema.Domain.ShowtimeAggregate;
using Cinema.Domain.TicketAggregate;
using Cinema.Domain.UserAggregate;
using Cinema.Infrastructure.Persistence.Write.Interceptors;
using Cinema.Infrastructure.Persistence.Write.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence.Write;

public class CinemaDbContext : DbContext
{
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Auditorium> Auditoriums => Set<Auditorium>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();

    public CinemaDbContext(
        DbContextOptions<CinemaDbContext> options) : base(options)
    {
        
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CinemaDbContext).Assembly);
        
        modelBuilder.Entity<SagaStateEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SagaType).HasMaxLength(100).IsRequired();
            e.Property(x => x.FailureReason).HasMaxLength(1000);
            e.HasIndex(x => new { x.SagaType, x.Status });
        });
        
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        
        base.OnConfiguring(optionsBuilder);
    }
}
