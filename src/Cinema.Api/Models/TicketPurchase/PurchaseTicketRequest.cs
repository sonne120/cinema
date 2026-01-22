using Cinema.Api.Models.Common;
using Cinema.Domain.PaymentAggregate.ValueObjects;

namespace Cinema.Api.Models.TicketPurchase;

public class PurchaseTicketRequest
{
    public Guid ShowtimeId { get; set; }
    public Guid CustomerId { get; set; }
    public List<SeatDto> Seats { get; set; } = new();
    public PaymentMethod PaymentMethod { get; set; }
    public string? CardNumber { get; set; }
    public string? CardHolderName { get; set; }
}
