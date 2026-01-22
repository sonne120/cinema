using Cinema.Domain.Common.Models;
using Cinema.Domain.PaymentAggregate;
using Cinema.Domain.PaymentAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Infrastructure.Persistence.Write.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PaymentId.Create(value))
            .ValueGeneratedNever();

        builder.Property(p => p.ReservationId)
            .IsRequired();

        builder.Property(p => p.CustomerId)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.Method)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.TransactionId)
            .HasMaxLength(100);

        builder.Property(p => p.FailureReason)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Configure Money value object for Amount
        builder.OwnsOne(p => p.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Amount")
                .HasPrecision(18, 2);
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3);
        });

        // Configure Money value object for RefundedAmount
        builder.OwnsOne(p => p.RefundedAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("RefundedAmount")
                .HasPrecision(18, 2);
            money.Property(m => m.Currency)
                .HasColumnName("RefundCurrency")
                .HasMaxLength(3);
        });


        builder.Ignore(p => p.DomainEvents);
    }
}
