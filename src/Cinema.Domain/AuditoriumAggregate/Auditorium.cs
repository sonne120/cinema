using Cinema.Domain.AuditoriumAggregate.ValueObjects;
using Cinema.Domain.Common.Models;
using Cinema.Domain.ReservationAggregate.ValueObjects;

namespace Cinema.Domain.AuditoriumAggregate;

public sealed class Auditorium : AggregateRoot<AuditoriumId>
{
    public string Name { get; private set; } = string.Empty;
    public int TotalSeats { get; private set; }
    public int Rows { get; private set; }
    public int SeatsPerRow { get; private set; }

    private Auditorium() : base(AuditoriumId.CreateUnique()) { }

    private Auditorium(
        AuditoriumId id,
        string name,
        int rows,
        int seatsPerRow) : base(id)
    {
        Name = name;
        Rows = rows;
        SeatsPerRow = seatsPerRow;
        TotalSeats = rows * seatsPerRow;
    }

    public static Result<Auditorium> Create(string name, int rows, int seatsPerRow)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Auditorium>("Name is required");

        if (rows <= 0)
            return Result.Failure<Auditorium>("Rows must be positive");

        if (seatsPerRow <= 0)
            return Result.Failure<Auditorium>("Seats per row must be positive");

        var auditoriumId = AuditoriumId.CreateUnique();
        return Result.Success(new Auditorium(auditoriumId, name, rows, seatsPerRow));
    }


    public IEnumerable<SeatNumber> GetAllSeats()
    {
        for (short row = 1; row <= Rows; row++)
        {
            for (short seat = 1; seat <= SeatsPerRow; seat++)
            {
                yield return SeatNumber.Create(row, seat);
            }
        }
    }
}
