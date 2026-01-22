using Cinema.Api.Models.Common;

namespace Cinema.Api.Models.TicketPurchase;

public class PurchaseTicketResponse
{
    public bool Success { get; set; }
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public Guid PaymentId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime ScreeningTime { get; set; }
    public List<SeatDto> Seats { get; set; } = new();
    public decimal TotalPrice { get; set; }
}
