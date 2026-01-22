using Cinema.MasterNode.Domain.Models;
using Cinema.MasterNode.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cinema.MasterNode.UnitTests.Persistence;

public class MasterDbContextTests : IDisposable
{
    private readonly MasterDbContext _context;

    public MasterDbContextTests()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MasterDbContext(options);
    }

    [Fact]
    public void DbSets_ShouldBeConfigured()
    {
        // Assert
        _context.Reservations.Should().NotBeNull();
        _context.ReservationSeats.Should().NotBeNull();
        _context.Showtimes.Should().NotBeNull();
    }

    [Fact]
    public async Task AddReservation_ShouldPersist()
    {
        // Arrange
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            ShowtimeId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = "Pending",
            TotalPrice = 25.00m
        };

        // Act
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.Reservations.FirstOrDefaultAsync();
        result.Should().NotBeNull();
        result!.Id.Should().Be(reservation.Id);
    }

    [Fact]
    public async Task AddShowtime_ShouldPersist()
    {
        // Arrange
        var showtime = new Showtime
        {
            Id = Guid.NewGuid(),
            MovieImdbId = "tt1234567",
            ScreeningTime = DateTime.UtcNow.AddDays(1),
            AuditoriumId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.Showtimes.Add(showtime);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.Showtimes.FirstOrDefaultAsync();
        result.Should().NotBeNull();
        result!.MovieImdbId.Should().Be("tt1234567");
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
