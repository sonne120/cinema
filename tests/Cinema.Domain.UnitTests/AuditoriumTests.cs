using Cinema.Domain.AuditoriumAggregate;
using Cinema.Domain.AuditoriumAggregate.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Cinema.Domain.UnitTests.AuditoriumAggregate;

public class AuditoriumTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateAuditorium()
    {
        var result = Auditorium.Create("Hall 1", 10, 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Hall 1");
        result.Value.Rows.Should().Be(10);
        result.Value.SeatsPerRow.Should().Be(20);
        result.Value.TotalSeats.Should().Be(200);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldReturnFailure()
    {
        var result = Auditorium.Create("", 10, 20);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Name");
    }

    [Fact]
    public void Create_WithZeroRows_ShouldReturnFailure()
    {
        var result = Auditorium.Create("Hall 1", 0, 20);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Rows");
    }

    [Fact]
    public void Create_WithZeroSeatsPerRow_ShouldReturnFailure()
    {
        var result = Auditorium.Create("Hall 1", 10, 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Seats");
    }

    [Fact]
    public void GetAllSeats_ShouldReturnAllSeats()
    {
        var auditorium = CreateValidAuditorium(3, 4);

        var seats = auditorium.GetAllSeats().ToList();

        seats.Should().HaveCount(12);
        seats.Should().Contain(s => s.Row == 1 && s.Number == 1);
        seats.Should().Contain(s => s.Row == 3 && s.Number == 4);
    }

    [Fact]
    public void GetAllSeats_ShouldReturnSeatsInOrder()
    {
        var auditorium = CreateValidAuditorium(2, 3);

        var seats = auditorium.GetAllSeats().ToList();

        seats.Should().HaveCount(6);
        seats[0].Row.Should().Be(1);
        seats[0].Number.Should().Be(1);
        seats[5].Row.Should().Be(2);
        seats[5].Number.Should().Be(3);
    }

    [Fact]
    public void TotalSeats_ShouldBeRowsTimesSeatsPerRow()
    {
        var auditorium = CreateValidAuditorium(5, 10);

        auditorium.TotalSeats.Should().Be(50);
    }

    private static Auditorium CreateValidAuditorium(int rows = 10, int seatsPerRow = 20)
    {
        var result = Auditorium.Create("Hall 1", rows, seatsPerRow);
        return result.Value;
    }
}

public class AuditoriumIdTests
{
    [Fact]
    public void CreateUnique_ShouldCreateNewId()
    {
        var id1 = AuditoriumId.CreateUnique();
        var id2 = AuditoriumId.CreateUnique();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Create_WithGuid_ShouldWrapGuid()
    {
        var guid = Guid.NewGuid();

        var id = AuditoriumId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ImplicitConversion_ToGuid_ShouldWork()
    {
        var id = AuditoriumId.CreateUnique();

        Guid guid = id;

        guid.Should().Be(id.Value);
    }
}
