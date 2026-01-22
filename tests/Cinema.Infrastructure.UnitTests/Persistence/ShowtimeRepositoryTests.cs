using Cinema.Domain.AuditoriumAggregate.ValueObjects;
using Cinema.Domain.ShowtimeAggregate;
using Cinema.Domain.ShowtimeAggregate.ValueObjects;
using Cinema.Infrastructure.Persistence.Write.Repositories;
using FluentAssertions;

namespace Cinema.Infrastructure.UnitTests.Persistence;

public class ShowtimeRepositoryTests : RepositoryTestBase
{
    private readonly ShowtimeRepository _repository;

    public ShowtimeRepositoryTests()
    {
        _repository = new ShowtimeRepository(Context);
    }

    [Fact]
    public async Task AddAsync_ShouldAddShowtime()
    {
        // Arrange
        var showtime = Showtime.Create(
            "tt1234567",
            DateTime.UtcNow.AddDays(1),
            AuditoriumId.Create(Guid.NewGuid()));

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
        var showtime1 = Showtime.Create("tt1234567", DateTime.UtcNow.AddDays(1), AuditoriumId.Create(Guid.NewGuid()));
        var showtime2 = Showtime.Create("tt7654321", DateTime.UtcNow.AddDays(2), AuditoriumId.Create(Guid.NewGuid()));

        await _repository.AddAsync(showtime1);
        await _repository.AddAsync(showtime2);
        await Context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }
}
