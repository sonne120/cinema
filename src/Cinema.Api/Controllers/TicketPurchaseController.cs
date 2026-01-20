using Cinema.Api.Models.Common;
using Cinema.Api.Models.TicketPurchase;
using Cinema.Application.Sagas;
using Cinema.Application.Sagas.TicketPurchase;
using Cinema.Domain.PaymentAggregate.ValueObjects;
using Cinema.Domain.ReservationAggregate.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketPurchaseController : ControllerBase
{
    private readonly TicketPurchaseSaga _saga;
    private readonly ISagaStateRepository _stateRepository;
    private readonly ILogger<TicketPurchaseController> _logger;

    public TicketPurchaseController(
        TicketPurchaseSaga saga,
        ISagaStateRepository stateRepository,
        ILogger<TicketPurchaseController> logger)
    {
        _saga = saga;
        _stateRepository = stateRepository;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PurchaseTicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PurchaseTicketResponse>> PurchaseTicket(
        [FromBody] PurchaseTicketRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Received ticket purchase request for showtime {ShowtimeId}", request.ShowtimeId);

        var command = new PurchaseTicketCommand(
            request.ShowtimeId,
            request.CustomerId,
            request.Seats.Select(s => SeatNumber.Create(s.Row, s.Number)).ToList(),
            request.PaymentMethod,
            request.CardNumber,
            request.CardHolderName);

        var result = await _saga.ExecuteAsync(command, ct);

        if (result.IsFailure)
        {
            return BadRequest(new ErrorResponse(result.Error, result.State?.SagaId));
        }

        var response = new PurchaseTicketResponse
        {
            Success = true,
            TicketId = result.Value!.TicketId,
            TicketNumber = result.Value.TicketNumber,
            ReservationId = result.Value.ReservationId,
            PaymentId = result.Value.PaymentId,
            MovieTitle = result.Value.MovieTitle,
            ScreeningTime = result.Value.ScreeningTime,
            Seats = result.Value.Seats.Select(s => new SeatDto { Row = s.Row, Number = s.Number }).ToList(),
            TotalPrice = result.Value.TotalPrice
        };

        return Ok(response);
    }

    [HttpGet("{sagaId}/status")]
    [ProducesResponseType(typeof(SagaStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SagaStatusResponse>> GetSagaStatus(Guid sagaId, CancellationToken ct)
    {
        var state = await _stateRepository.GetByIdAsync<TicketPurchaseSagaState>(sagaId, ct);
        if (state == null)
            return NotFound(new { error = "Saga not found" });

        return Ok(new SagaStatusResponse
        {
            SagaId = state.SagaId,
            Status = state.Status.ToString(),
            CurrentStep = state.CurrentStep,
            TotalSteps = state.TotalSteps,
            FailureReason = state.FailureReason,
            CreatedAt = state.CreatedAt,
            CompletedAt = state.CompletedAt,
            TicketId = state.TicketId,
            TicketNumber = state.TicketNumber,
            StepLogs = state.StepLogs.Select(l => new StepLogDto
            {
                StepName = l.StepName,
                Success = l.Success,
                Message = l.Message,
                Timestamp = l.Timestamp
            }).ToList()
        });
    }
}

