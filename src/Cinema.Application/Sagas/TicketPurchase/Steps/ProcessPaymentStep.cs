using Cinema.Domain.Common.Models;
using Cinema.Domain.PaymentAggregate;
using Cinema.Domain.PaymentAggregate.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Cinema.Application.Sagas.TicketPurchase.Steps;
public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ProcessPaymentAsync(
        decimal amount,
        string currency,
        PaymentMethod method,
        string? cardNumber,
        string? cardHolderName,
        CancellationToken ct);

    Task<RefundResult> RefundAsync(
        string transactionId,
        decimal amount,
        string currency,
        CancellationToken ct);
}

public record PaymentGatewayResult(bool Success, string? TransactionId, string? ErrorMessage);

public record RefundResult(bool Success, string? RefundId, string? ErrorMessage);

public class ProcessPaymentStep : ISagaStep<TicketPurchaseSagaState>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<ProcessPaymentStep> _logger;

    public string StepName => "ProcessPayment";
    public int StepOrder => 2;

    public ProcessPaymentStep(
        IPaymentRepository paymentRepository,
        IPaymentGateway paymentGateway,
        ILogger<ProcessPaymentStep> logger)
    {
        _paymentRepository = paymentRepository;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: Executing {StepName}", state.SagaId, StepName);

        try
        {
            if (!state.ReservationId.HasValue)
                return StepResult.Failure("Reservation ID is required");

            var amount = Money.Create(state.TotalPrice);

            // Create payment record
            var paymentResult = Payment.Create(
                state.ReservationId.Value,
                state.CustomerId,
                amount,
                state.PaymentMethod);

            if (paymentResult.IsFailure)
                return StepResult.Failure(paymentResult.Error);

            var payment = paymentResult.Value;
            payment.StartProcessing();

            // Save to database
            await _paymentRepository.AddAsync(payment, ct);

            // Process with external gateway
            var gatewayResult = await _paymentGateway.ProcessPaymentAsync(
                state.TotalPrice,
                "USD",
                state.PaymentMethod,
                state.CardNumber,
                state.CardHolderName,
                ct);

            if (gatewayResult.Success && !string.IsNullOrEmpty(gatewayResult.TransactionId))
            {
                payment.Complete(gatewayResult.TransactionId);
                state.TransactionId = gatewayResult.TransactionId;
            }
            else
            {
                payment.Decline(gatewayResult.ErrorMessage ?? "Payment declined");
                await _paymentRepository.UpdateAsync(payment, ct);
                return StepResult.Failure(gatewayResult.ErrorMessage ?? "Payment declined");
            }

            await _paymentRepository.UpdateAsync(payment, ct);

            // Update state
            state.PaymentId = payment.Id.Value;
            state.AmountCharged = state.TotalPrice;
            state.PaymentProcessed = true;
            state.LogStep(StepName, true, $"Payment processed, TransactionId: {gatewayResult.TransactionId}");

            _logger.LogInformation("Saga {SagaId}: {StepName} completed - PaymentId: {PaymentId}",
                state.SagaId, StepName, payment.Id.Value);

            return StepResult.Success($"Payment processed: {gatewayResult.TransactionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId}: {StepName} failed", state.SagaId, StepName);
            state.LogStep(StepName, false, ex.Message);
            return StepResult.Failure(ex.Message);
        }
    }

    public async Task<StepResult> CompensateAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: Compensating {StepName}", state.SagaId, StepName);

        try
        {
            if (!state.PaymentId.HasValue)
                return StepResult.Success("No payment to refund");

            var paymentId = PaymentId.Create(state.PaymentId.Value);
            var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment == null)
                return StepResult.Success("Payment not found");

            if (!payment.CanBeRefunded())
                return StepResult.Success("Payment cannot be refunded");

            // Refund via gateway
            if (!string.IsNullOrEmpty(state.TransactionId))
            {
                var refundResult = await _paymentGateway.RefundAsync(
                    state.TransactionId,
                    state.AmountCharged,
                    "USD",
                    ct);

                if (!refundResult.Success)
                {
                    _logger.LogWarning("Saga {SagaId}: Refund failed - {Error}",
                        state.SagaId, refundResult.ErrorMessage);
                }
            }

            payment.Refund("Saga compensation");
            await _paymentRepository.UpdateAsync(payment, ct);

            state.PaymentProcessed = false;
            _logger.LogInformation("Saga {SagaId}: {StepName} compensated", state.SagaId, StepName);

            return StepResult.Success("Payment refunded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId}: {StepName} compensation failed", state.SagaId, StepName);
            return StepResult.Failure(ex.Message);
        }
    }

    public bool ShouldCompensate(TicketPurchaseSagaState state)
    {
        return state.PaymentProcessed && state.PaymentId.HasValue;
    }
}
