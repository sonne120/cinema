using System.Text.Json;
using Cinema.Application.Sagas;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence.Write.Repositories;

public class SagaStateRepository : ISagaStateRepository
{
    private readonly CinemaDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SagaStateRepository(CinemaDbContext context) => _context = context;

    public async Task<TState?> GetByIdAsync<TState>(Guid sagaId, CancellationToken ct = default)
        where TState : class, ISagaState
    {
        var entity = await _context.SagaStates.FirstOrDefaultAsync(x => x.Id == sagaId, ct);
        if (entity == null) return null;

        var state = JsonSerializer.Deserialize<TState>(entity.SerializedData, JsonOptions);
        return state;
    }

    public async Task<IEnumerable<TState>> GetIncompleteSagasAsync<TState>(CancellationToken ct = default)
        where TState : class, ISagaState
    {
        var sagaType = typeof(TState).Name.Replace("State", "");
        var entities = await _context.SagaStates
            .Where(x => x.SagaType == sagaType &&
                        x.Status != (int)SagaStatus.Completed &&
                        x.Status != (int)SagaStatus.Compensated &&
                        x.Status != (int)SagaStatus.Failed)
            .ToListAsync(ct);

        return entities.Select(e => JsonSerializer.Deserialize<TState>(e.SerializedData, JsonOptions)!)
                       .Where(s => s != null);
    }

    public async Task<IEnumerable<TState>> GetTimedOutSagasAsync<TState>(CancellationToken ct = default)
        where TState : class, ISagaState
    {
        var sagaType = typeof(TState).Name.Replace("State", "");
        var cutoff = DateTime.UtcNow.AddMinutes(-15);

        var entities = await _context.SagaStates
            .Where(x => x.SagaType == sagaType &&
                        x.Status != (int)SagaStatus.Completed &&
                        x.Status != (int)SagaStatus.Compensated &&
                        x.Status != (int)SagaStatus.Failed &&
                        x.CreatedAt < cutoff)
            .ToListAsync(ct);

        return entities.Select(e => JsonSerializer.Deserialize<TState>(e.SerializedData, JsonOptions)!)
                       .Where(s => s != null);
    }

    public async Task SaveAsync<TState>(TState state, CancellationToken ct = default)
        where TState : class, ISagaState
    {
        var entity = new SagaStateEntity
        {
            Id = state.SagaId,
            SagaType = state.SagaType,
            Status = (int)state.Status,
            CurrentStep = state.CurrentStep,
            TotalSteps = state.TotalSteps,
            SerializedData = JsonSerializer.Serialize(state, JsonOptions),
            FailureReason = state.FailureReason,
            RetryCount = state.RetryCount,
            CreatedAt = state.CreatedAt,
            CompletedAt = state.CompletedAt,
            LastUpdatedAt = DateTime.UtcNow
        };

        await _context.SagaStates.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync<TState>(TState state, CancellationToken ct = default)
        where TState : class, ISagaState
    {
        var entity = await _context.SagaStates.FirstOrDefaultAsync(x => x.Id == state.SagaId, ct);
        if (entity != null)
        {
            entity.Status = (int)state.Status;
            entity.CurrentStep = state.CurrentStep;
            entity.SerializedData = JsonSerializer.Serialize(state, JsonOptions);
            entity.FailureReason = state.FailureReason;
            entity.RetryCount = state.RetryCount;
            entity.CompletedAt = state.CompletedAt;
            entity.LastUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }
        else
        {
            await SaveAsync(state, ct);
        }
    }
}
