using Cinema.Domain.Common.Models;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Cinema.Domain.TicketAggregate;
using Cinema.Domain.TicketAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Infrastructure.Persistence.Write.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value,
                value => TicketId.Create(value))
            .ValueGeneratedNever();

        builder.Property(t => t.TicketNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(t => t.TicketNumber)
            .IsUnique();

        builder.Property(t => t.ReservationId)
            .IsRequired();

        builder.Property(t => t.PaymentId)
            .IsRequired();

        builder.Property(t => t.ShowtimeId)
            .IsRequired();

        builder.Property(t => t.CustomerId)
            .IsRequired();

        builder.Property(t => t.MovieTitle)
            .HasMaxLength(200);

        builder.Property(t => t.AuditoriumName)
            .HasMaxLength(100);

        builder.Property(t => t.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.QrCode)
            .HasMaxLength(500);

        builder.Property(t => t.IssuedAt)
            .IsRequired();

        // Configure Money value object for TotalPrice
        builder.OwnsOne(t => t.TotalPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalPrice")
                .HasPrecision(18, 2);
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3);
        });

        // Configure Seats collection
        builder.OwnsMany(t => t.Seats, seat =>
        {
            seat.ToTable("TicketSeats");
            seat.WithOwner().HasForeignKey("TicketId");
            seat.Property(s => s.Row).HasColumnName("Row");
            seat.Property(s => s.Number).HasColumnName("Number");
        });

        // Ignore domain events
        builder.Ignore(t => t.DomainEvents);
    }
}
