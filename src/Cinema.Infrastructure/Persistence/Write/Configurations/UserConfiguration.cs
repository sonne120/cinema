using Cinema.Domain.UserAggregate;
using Cinema.Domain.UserAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Infrastructure.Persistence.Write.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value))
            .ValueGeneratedNever();

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.LastLoginAt);

        builder.Property(u => u.RefreshToken)
            .HasMaxLength(500);

        builder.Property(u => u.RefreshTokenExpiryTime);

        builder.Ignore(u => u.DomainEvents);
    }
}
