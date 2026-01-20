using Cinema.Domain.Common.Models;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Cinema.Domain.TicketAggregate;
using Cinema.Domain.TicketAggregate.Events;
using Cinema.Domain.TicketAggregate.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Cinema.Domain.UnitTests.TicketAggregate;

public class TicketTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateTicket()
    {
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var showtimeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var seats = CreateValidSeats();
        var price = Money.Create(50m);

        var result = Ticket.Create(
            reservationId,
            paymentId,
            showtimeId,
            customerId,
            "Inception",
            DateTime.UtcNow.AddDays(1),
            "Hall 1",
            seats,
            price);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReservationId.Should().Be(reservationId);
        result.Value.PaymentId.Should().Be(paymentId);
        result.Value.ShowtimeId.Should().Be(showtimeId);
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.MovieTitle.Should().Be("Inception");
        result.Value.AuditoriumName.Should().Be("Hall 1");
        result.Value.Seats.Should().HaveCount(2);
        result.Value.TotalPrice.Should().Be(price);
        result.Value.Status.Should().Be(TicketStatus.Issued);
        result.Value.TicketNumber.Should().StartWith("TKT-");
        result.Value.DomainEvents.Should().ContainSingle(e => e is TicketIssuedEvent);
    }

    [Fact]
    public void Create_WithEmptyMovieTitle_ShouldReturnFailure()
    {
        var result = Ticket.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "",
            DateTime.UtcNow.AddDays(1),
            "Hall 1",
            CreateValidSeats(),
            Money.Create(50m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Movie title");
    }

    [Fact]
    public void Create_WithEmptySeats_ShouldReturnFailure()
    {
        var result = Ticket.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Movie",
            DateTime.UtcNow.AddDays(1),
            "Hall 1",
            new List<SeatNumber>(),
            Money.Create(50m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("seat");
    }

    [Fact]
    public void Use_FromIssued_ShouldSucceed()
    {
        var ticket = CreateValidTicket();

        var result = ticket.Use();

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Used);
        ticket.UsedAt.Should().NotBeNull();
        ticket.DomainEvents.Should().Contain(e => e is TicketUsedEvent);
    }

    [Fact]
    public void Use_FromCancelled_ShouldFail()
    {
        var ticket = CreateValidTicket();
        ticket.Cancel();

        var result = ticket.Use();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cancel_FromIssued_ShouldSucceed()
    {
        var ticket = CreateValidTicket();

        var result = ticket.Cancel();

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Cancelled);
        ticket.DomainEvents.Should().Contain(e => e is TicketCancelledEvent);
    }

    [Fact]
    public void Cancel_FromUsed_ShouldFail()
    {
        var ticket = CreateValidTicket();
        ticket.Use();

        var result = ticket.Cancel();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Refund_FromIssued_ShouldSucceed()
    {
        var ticket = CreateValidTicket();

        var result = ticket.Refund();

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Refunded);
        ticket.DomainEvents.Should().Contain(e => e is TicketRefundedEvent);
    }

    [Fact]
    public void Refund_FromUsed_ShouldFail()
    {
        var ticket = CreateValidTicket();
        ticket.Use();

        var result = ticket.Refund();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithIssuedStatus_ShouldReturnTrue()
    {
        var ticket = CreateValidTicket();

        var result = ticket.Validate();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithCancelledStatus_ShouldReturnFalse()
    {
        var ticket = CreateValidTicket();
        ticket.Cancel();

        var result = ticket.Validate();

        result.Value.Should().BeFalse();
    }

    [Fact]
    public void QrCode_ShouldContainTicketInfo()
    {
        var ticket = CreateValidTicket();

        var qrCode = ticket.QrCode;

        qrCode.Should().StartWith("QR:");
        qrCode.Should().Contain(ticket.Id.Value.ToString());
        qrCode.Should().Contain(ticket.TicketNumber);
    }

    private static List<SeatNumber> CreateValidSeats()
    {
        return new List<SeatNumber>
        {
            SeatNumber.Create(5, 10),
            SeatNumber.Create(5, 11)
        };
    }

    private static Ticket CreateValidTicket()
    {
        var result = Ticket.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Inception",
            DateTime.UtcNow.AddDays(1),
            "Hall 1",
            CreateValidSeats(),
            Money.Create(50m));
        return result.Value;
    }
}

public class TicketIdTests
{
    [Fact]
    public void CreateUnique_ShouldCreateNewId()
    {
        var id1 = TicketId.CreateUnique();
        var id2 = TicketId.CreateUnique();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Create_WithGuid_ShouldWrapGuid()
    {
        var guid = Guid.NewGuid();

        var id = TicketId.Create(guid);

        id.Value.Should().Be(guid);
    }
}
