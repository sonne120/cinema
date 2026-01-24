using Cinema.Domain.AuditoriumAggregate.ValueObjects;
using Cinema.Domain.ShowtimeAggregate;
using Cinema.Domain.ShowtimeAggregate.ValueObjects;
using Cinema.Infrastructure.Persistence.Write.Repositories;
using FluentAssertions;
using Xunit;

namespace Cinema.Infrastructure.UnitTests.Persistence;

public class ShowtimeRepositoryTests : RepositoryTestBase
{
    private readonly ShowtimeRepository _repository;

    public ShowtimeRepositoryTests()
    {
        _repository = new ShowtimeRepository(Context);
    }

    private static Showtime CreateTestShowtime(string imdbId = "tt1234567", string title = "Test Movie", int daysFromNow = 1)
    {
        var movieDetails = MovieDetails.Create(imdbId, title);
        var screeningTime = ScreeningTime.Create(DateTime.UtcNow.AddDays(daysFromNow));
        return Showtime.Create(movieDetails, screeningTime, Guid.NewGuid());
    }

    [Fact]
    public async Task AddAsync_ShouldAddShowtime()
    {
        // Arrange
        var showtime = CreateTestShowtime();

        // Act
        await _repository.AddAsync(showtime);
        await Context.SaveChangesAsync();

        // Assert
        var result = await _repository.GetByIdAsync(showtime.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(showtime.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = ShowtimeId.Create(Guid.NewGuid());

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllShowtimes()
    {
        // Arrange
        var showtime1 = CreateTestShowtime("tt1234567", "Test Movie 1", 1);
        var showtime2 = CreateTestShowtime("tt7654321", "Test Movie 2", 2);

        await _repository.AddAsync(showtime1);
        await _repository.AddAsync(showtime2);
        await Context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }
}
