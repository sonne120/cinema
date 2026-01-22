using Cinema.Application.Sagas.TicketPurchase.Steps;
using Cinema.Domain.AuditoriumAggregate;
using Cinema.Domain.AuditoriumAggregate.ValueObjects;
using Cinema.Domain.PaymentAggregate.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Cinema.Infrastructure.Services;


public class MockPaymentGateway : IPaymentGateway
{
    private readonly ILogger<MockPaymentGateway> _logger;

    public MockPaymentGateway(ILogger<MockPaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<PaymentGatewayResult> ProcessPaymentAsync(
        decimal amount,
        string currency,
        PaymentMethod method,
        string? cardNumber,
        string? cardHolderName,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing payment of {Amount} {Currency} via {Method}", 
            amount, currency, method);

        // Simulate payment processing
        var transactionId = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}";
        
        // Simulate occasional payment failures
        if (new Random().Next(10) == 0)
        {
            return Task.FromResult(new PaymentGatewayResult(false, null, "Payment declined by issuing bank"));
        }

        return Task.FromResult(new PaymentGatewayResult(true, transactionId, null));
    }

    public Task<RefundResult> RefundAsync(
        string transactionId,
        decimal amount,
        string currency,
        CancellationToken ct)
    {
        _logger.LogInformation("Refunding {Amount} {Currency} for transaction {TransactionId}", 
            amount, currency, transactionId);

        var refundId = $"REF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}";
        return Task.FromResult(new RefundResult(true, refundId, null));
    }
}

public class AuditoriumService : IAuditoriumService
{
    private readonly IAuditoriumRepository _auditoriumRepository;

    public AuditoriumService(IAuditoriumRepository auditoriumRepository)
    {
        _auditoriumRepository = auditoriumRepository;
    }

    public async Task<string> GetAuditoriumNameAsync(Guid auditoriumId, CancellationToken ct)
    {
        if (auditoriumId == Guid.Empty)
            return "Main Auditorium"; // Default

        var auditorium = await _auditoriumRepository.GetByIdAsync(AuditoriumId.Create(auditoriumId), ct);
        return auditorium?.Name ?? "Unknown Auditorium";
    }
}

public class MockNotificationService : INotificationService
{
    private readonly ILogger<MockNotificationService> _logger;

    public MockNotificationService(ILogger<MockNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendTicketEmailAsync(
        Guid customerId, 
        string ticketNumber, 
        string movieTitle,
        DateTime screeningTime, 
        string seats, 
        string qrCode, 
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Sending ticket email to customer {CustomerId}: Ticket {TicketNumber} for {MovieTitle} at {ScreeningTime}",
            customerId, ticketNumber, movieTitle, screeningTime);
        
        return Task.CompletedTask;
    }

    public Task SendRefundNotificationAsync(
        Guid customerId, 
        string ticketNumber,
        decimal refundAmount, 
        string reason, 
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Sending refund notification to customer {CustomerId}: {Amount} refunded for ticket {TicketNumber}",
            customerId, refundAmount, ticketNumber);
        
        return Task.CompletedTask;
    }
}
