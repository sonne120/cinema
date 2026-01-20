using Cinema.Domain.Common.Models;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Cinema.Domain.TicketAggregate.Events;
using Cinema.Domain.TicketAggregate.ValueObjects;

namespace Cinema.Domain.TicketAggregate;

public sealed class Ticket : AggregateRoot<TicketId>
{
    private readonly List<SeatNumber> _seats = new();

    public string TicketNumber { get; private set; } = string.Empty;
    public Guid ReservationId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid ShowtimeId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string MovieTitle { get; private set; } = string.Empty;
    public DateTime ScreeningTime { get; private set; }
    public string AuditoriumName { get; private set; } = string.Empty;
    public Money TotalPrice { get; private set; } = Money.Zero();
    public TicketStatus Status { get; private set; }
    public string QrCode { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    public IReadOnlyCollection<SeatNumber> Seats => _seats.AsReadOnly();

    private Ticket() : base(TicketId.CreateUnique()) { }

    private Ticket(
        TicketId id,
        Guid reservationId,
        Guid paymentId,
        Guid showtimeId,
        Guid customerId,
        string movieTitle,
        DateTime screeningTime,
        string auditoriumName,
        IEnumerable<SeatNumber> seats,
        Money totalPrice) : base(id)
    {
        TicketNumber = GenerateTicketNumber();
        ReservationId = reservationId;
        PaymentId = paymentId;
        ShowtimeId = showtimeId;
        CustomerId = customerId;
        MovieTitle = movieTitle;
        ScreeningTime = screeningTime;
        AuditoriumName = auditoriumName;
        TotalPrice = totalPrice;
        Status = TicketStatus.Issued;
        IssuedAt = DateTime.UtcNow;
        _seats.AddRange(seats);
        QrCode = GenerateQrCode();
    }


    public static Result<Ticket> Create(
        Guid reservationId,
        Guid paymentId,
        Guid showtimeId,
        Guid customerId,
        string movieTitle,
        DateTime screeningTime,
        string auditoriumName,
        IEnumerable<SeatNumber> seats,
        Money totalPrice)
    {
        var seatList = seats.ToList();

        if (!seatList.Any())
            return Result.Failure<Ticket>("At least one seat is required");

        if (string.IsNullOrWhiteSpace(movieTitle))
            return Result.Failure<Ticket>("Movie title is required");

        var ticketId = TicketId.CreateUnique();
        var ticket = new Ticket(
            ticketId,
            reservationId,
            paymentId,
            showtimeId,
            customerId,
            movieTitle,
            screeningTime,
            auditoriumName,
            seatList,
            totalPrice);

        ticket.RaiseDomainEvent(new TicketIssuedEvent(
            ticketId,
            ticket.TicketNumber,
            reservationId,
            paymentId,
            showtimeId,
            customerId,
            movieTitle,
            screeningTime,
            seatList));

        return Result.Success(ticket);
    }

    public Result Use()
    {
        if (Status != TicketStatus.Issued)
            return Result.Failure("Ticket cannot be used in current state");

        if (DateTime.UtcNow > ScreeningTime.AddHours(3))
            return Result.Failure("Ticket has expired - screening has ended");

        Status = TicketStatus.Used;
        UsedAt = DateTime.UtcNow;

        RaiseDomainEvent(new TicketUsedEvent(Id, TicketNumber, UsedAt.Value));
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status != TicketStatus.Issued)
            return Result.Failure("Only issued tickets can be cancelled");

        Status = TicketStatus.Cancelled;

        RaiseDomainEvent(new TicketCancelledEvent(Id, TicketNumber));
        return Result.Success();
    }

    public Result Refund()
    {
        if (!CanBeRefunded())
            return Result.Failure("Ticket cannot be refunded");

        Status = TicketStatus.Refunded;

        RaiseDomainEvent(new TicketRefundedEvent(Id, TicketNumber));
        return Result.Success();
    }

    public bool CanBeRefunded()
    {
        return Status == TicketStatus.Issued && DateTime.UtcNow < ScreeningTime.AddHours(-1);
    }

 
    public Result<bool> Validate()
    {
        if (Status != TicketStatus.Issued)
            return Result.Success(false);

        if (DateTime.UtcNow > ScreeningTime.AddHours(3))
            return Result.Success(false);

        return Result.Success(true);
    }

    private static string GenerateTicketNumber()
    {
        return $"TKT-{DateTime.UtcNow:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}";
    }

    private string GenerateQrCode()
    {
        return $"QR:{Id.Value}:{TicketNumber}:{ShowtimeId}";
    }
}
