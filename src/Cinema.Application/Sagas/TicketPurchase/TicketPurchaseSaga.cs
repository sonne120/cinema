using Cinema.Application.Common.Interfaces.Messaging;
using Cinema.Application.Sagas.TicketPurchase.Events;
using Cinema.Application.Sagas.TicketPurchase.Steps;
using Microsoft.Extensions.Logging;

namespace Cinema.Application.Sagas.TicketPurchase;
public class TicketPurchaseSaga : ISaga<TicketPurchaseSagaState, PurchaseTicketCommand, PurchaseTicketResult>
{
    private readonly ISagaStateRepository _stateRepository;
    private readonly IEventBus _eventBus;
    private readonly ReserveSeatsStep _reserveSeatsStep;
    private readonly ProcessPaymentStep _processPaymentStep;
    private readonly ConfirmReservationStep _confirmReservationStep;
    private readonly IssueTicketStep _issueTicketStep;
    private readonly ILogger<TicketPurchaseSaga> _logger;
    private readonly List<ISagaStep<TicketPurchaseSagaState>> _steps;

    public string SagaName => "TicketPurchaseSaga";

    public TicketPurchaseSaga(
        ISagaStateRepository stateRepository, 
        IEventBus eventBus,
        ReserveSeatsStep reserveSeatsStep, 
        ProcessPaymentStep processPaymentStep,
        ConfirmReservationStep confirmReservationStep, 
        IssueTicketStep issueTicketStep,
        ILogger<TicketPurchaseSaga> logger)
    {
        _stateRepository = stateRepository;
        _eventBus = eventBus;
        _reserveSeatsStep = reserveSeatsStep;
        _processPaymentStep = processPaymentStep;
        _confirmReservationStep = confirmReservationStep;
        _issueTicketStep = issueTicketStep;
        _logger = logger;
        _steps = new List<ISagaStep<TicketPurchaseSagaState>>
        {
            _reserveSeatsStep, 
            _processPaymentStep, 
            _confirmReservationStep, 
            _issueTicketStep
        };
    }

    public async Task<SagaResult<PurchaseTicketResult>> ExecuteAsync(PurchaseTicketCommand command, CancellationToken ct)
    {
        var state = new TicketPurchaseSagaState
        {
            ShowtimeId = command.ShowtimeId,
            CustomerId = command.CustomerId,
            Seats = command.Seats,
            PaymentMethod = command.PaymentMethod,
            CardNumber = command.CardNumber,
            CardHolderName = command.CardHolderName
        };

        _logger.LogInformation("Saga {SagaId}: Starting {SagaName}", state.SagaId, SagaName);
        await _stateRepository.SaveAsync(state, ct);
        await _eventBus.PublishAsync(new TicketPurchaseSagaStartedEvent(
            state.SagaId, state.ShowtimeId, state.CustomerId, state.Seats.Count), ct);

        var result = await ExecuteStepsAsync(state, ct);
        return result;
    }

    private async Task<SagaResult<PurchaseTicketResult>> ExecuteStepsAsync(
        TicketPurchaseSagaState state, CancellationToken ct)
    {
        state.Status = SagaStatus.Running;
        foreach (var step in _steps.OrderBy(s => s.StepOrder))
        {
            if (state.IsTimedOut)
            {
                state.Status = SagaStatus.TimedOut;
                await CompensateAsync(state, ct);
                return SagaResult<PurchaseTicketResult>.Failure("Saga timed out", state);
            }

            _logger.LogInformation("Saga {SagaId}: Executing step {Step}/{Total} - {StepName}",
                state.SagaId, step.StepOrder, state.TotalSteps, step.StepName);

            var stepResult = await step.ExecuteAsync(state, ct);
            await _eventBus.PublishAsync(new TicketPurchaseSagaStepCompletedEvent(
                state.SagaId, step.StepOrder, step.StepName, stepResult.IsSuccess,
                stepResult.IsSuccess ? stepResult.Message : stepResult.Error), ct);

            if (stepResult.IsFailure)
            {
                state.FailureReason = stepResult.Error;
                await CompensateAsync(state, ct);
                return SagaResult<PurchaseTicketResult>.Failure($"Compensated: {stepResult.Error}", state);
            }

            state.CurrentStep = step.StepOrder;
            await SaveStateAsync(state, ct);
        }

        state.Status = SagaStatus.Completed;
        state.CompletedAt = DateTime.UtcNow;
        await SaveStateAsync(state, ct);

        await _eventBus.PublishAsync(new TicketPurchaseSagaCompletedEvent(
            state.SagaId, state.TicketId!.Value, state.TicketNumber!,
            state.ReservationId!.Value, state.PaymentId!.Value), ct);

        var result = new PurchaseTicketResult(
            state.TicketId!.Value, 
            state.TicketNumber!, 
            state.ReservationId!.Value,
            state.PaymentId!.Value, 
            state.MovieTitle, 
            state.ScreeningTime,
            state.Seats, 
            state.TotalPrice);

        return SagaResult<PurchaseTicketResult>.Success(result, state);
    }

    public async Task<SagaResult> ResumeAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        if (state.IsTimedOut)
        {
            state.Status = SagaStatus.TimedOut;
            return await CompensateAsync(state, ct);
        }
        var result = await ExecuteStepsAsync(state, ct);
        return result.IsSuccess ? SagaResult.Success(state) : SagaResult.Failure(result.Error, state);
    }

    public async Task<SagaResult> CompensateAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        state.Status = SagaStatus.Compensating;
        await _eventBus.PublishAsync(new TicketPurchaseSagaCompensationStartedEvent(
            state.SagaId, state.FailureReason ?? "Unknown"), ct);

        foreach (var step in _steps.OrderByDescending(s => s.StepOrder))
        {
            if (step.ShouldCompensate(state))
            {
                _logger.LogInformation("Saga {SagaId}: Compensating {StepName}", state.SagaId, step.StepName);
                await step.CompensateAsync(state, ct);
            }
        }

        state.Status = SagaStatus.Compensated;
        state.CompletedAt = DateTime.UtcNow;
        await SaveStateAsync(state, ct);

        await _eventBus.PublishAsync(new TicketPurchaseSagaCompensatedEvent(
            state.SagaId, state.PaymentProcessed, state.SeatsReserved, state.TicketIssued), ct);

        return SagaResult.Success(state);
    }

    private async Task SaveStateAsync(TicketPurchaseSagaState state, CancellationToken ct)
    {
        state.LastUpdatedAt = DateTime.UtcNow;
        await _stateRepository.UpdateAsync(state, ct);
    }
}
