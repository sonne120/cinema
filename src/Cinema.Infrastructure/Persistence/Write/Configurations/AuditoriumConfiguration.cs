using Cinema.Domain.AuditoriumAggregate;
using Cinema.Domain.AuditoriumAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Infrastructure.Persistence.Write.Configurations;

public class AuditoriumConfiguration : IEntityTypeConfiguration<Auditorium>
{
    public void Configure(EntityTypeBuilder<Auditorium> builder)
    {
        builder.ToTable("Auditoriums");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => AuditoriumId.Create(value))
            .ValueGeneratedNever();

        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.TotalSeats)
            .IsRequired();

        builder.Property(a => a.Rows)
            .IsRequired();

        builder.Property(a => a.SeatsPerRow)
            .IsRequired();
        builder.Ignore(a => a.DomainEvents);
    }
}
