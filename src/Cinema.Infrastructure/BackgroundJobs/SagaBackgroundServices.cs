using Cinema.Application.Common.Interfaces.Persistence;
using Cinema.Application.Sagas;
using Cinema.Application.Sagas.TicketPurchase;
using Cinema.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cinema.Infrastructure.BackgroundJobs;

public class SagaRecoveryService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SagaRecoveryService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public SagaRecoveryService(IServiceProvider serviceProvider, ILogger<SagaRecoveryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverIncompleteSagasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SagaRecoveryService");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RecoverIncompleteSagasAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISagaStateRepository>();
        var saga = scope.ServiceProvider.GetRequiredService<TicketPurchaseSaga>();

        var incompleteSagas = await repository.GetIncompleteSagasAsync<TicketPurchaseSagaState>(ct);
        foreach (var state in incompleteSagas)
        {
            if (DateTime.UtcNow - state.LastUpdatedAt < TimeSpan.FromMinutes(5))
                continue; // Still being processed

            _logger.LogInformation("Recovering saga {SagaId}", state.SagaId);

            if (state.IsTimedOut)
            {
                state.Status = SagaStatus.TimedOut;
                await saga.CompensateAsync(state, ct);
            }
            else
            {
                await saga.ResumeAsync(state, ct);
            }
        }
    }
}
public class ReservationExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationExpirationService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public ReservationExpirationService(IServiceProvider serviceProvider, ILogger<ReservationExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReservationExpirationService");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredReservationsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var reservationRepo = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var showtimeRepo = scope.ServiceProvider.GetRequiredService<IShowtimeRepository>();

        var expiredReservations = await reservationRepo.GetExpiredReservationsAsync(ct);
        foreach (var reservation in expiredReservations)
        {
            _logger.LogInformation("Expiring reservation {ReservationId}", reservation.Id);

            reservation.Expire();
            reservationRepo.Update(reservation);

            var showtime = await showtimeRepo.GetByIdAsync(reservation.ShowtimeId, ct);
            if (showtime != null)
            {
                showtimeRepo.Update(showtime);
            }
        }
    }
}

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OutboxProcessor");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(100)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                _logger.LogDebug("Publishing event {EventType}", message.Type);
                message.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                _logger.LogWarning(ex, "Failed to process outbox message {MessageId}", message.Id);
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
