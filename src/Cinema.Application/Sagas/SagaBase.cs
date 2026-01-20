using Cinema.Domain.Common.Models;

namespace Cinema.Application.Sagas;

public enum SagaStatus
{
    Started,
    Running,
    Completed,
    Compensating,
    Compensated,
    Failed,
    TimedOut
}

public interface ISagaState
{
    Guid SagaId { get; }
    string SagaType { get; }
    SagaStatus Status { get; set; }
    int CurrentStep { get; set; }
    int TotalSteps { get; }
    string? FailureReason { get; set; }
    int RetryCount { get; set; }
    DateTime CreatedAt { get; }
    DateTime? CompletedAt { get; set; }
    DateTime LastUpdatedAt { get; set; }
    TimeSpan Timeout { get; }
    string? SerializedData { get; set; }
}


public abstract class SagaState : ISagaState
{
    public Guid SagaId { get; protected set; } = Guid.NewGuid();
    public abstract string SagaType { get; }
    public SagaStatus Status { get; set; } = SagaStatus.Started;
    public int CurrentStep { get; set; } = 0;
    public abstract int TotalSteps { get; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public abstract TimeSpan Timeout { get; }
    public string? SerializedData { get; set; }

    public bool IsTimedOut => DateTime.UtcNow - CreatedAt > Timeout;
    public bool IsCompleted => Status == SagaStatus.Completed ||
                               Status == SagaStatus.Compensated ||
                               Status == SagaStatus.Failed;
}


public class SagaResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public ISagaState? State { get; }

    protected SagaResult(bool isSuccess, string error, ISagaState? state)
    {
        IsSuccess = isSuccess;
        Error = error;
        State = state;
    }

    public static SagaResult Success(ISagaState state) => new(true, string.Empty, state);
    public static SagaResult Failure(string error, ISagaState? state = null) => new(false, error, state);
}

public class SagaResult<T> : SagaResult
{
    public T? Value { get; }

    private SagaResult(T? value, bool isSuccess, string error, ISagaState? state)
        : base(isSuccess, error, state)
    {
        Value = value;
    }

    public static SagaResult<T> Success(T value, ISagaState state) => new(value, true, string.Empty, state);
    public new static SagaResult<T> Failure(string error, ISagaState? state = null) => new(default, false, error, state);
}

public class StepResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public string Message { get; }

    private StepResult(bool isSuccess, string error, string message)
    {
        IsSuccess = isSuccess;
        Error = error;
        Message = message;
    }

    public static StepResult Success(string message = "") => new(true, string.Empty, message);
    public static StepResult Failure(string error) => new(false, error, string.Empty);
}


public interface ISagaStep<TState> where TState : ISagaState
{
    string StepName { get; }
    int StepOrder { get; }
    Task<StepResult> ExecuteAsync(TState state, CancellationToken ct);
    Task<StepResult> CompensateAsync(TState state, CancellationToken ct);
    bool ShouldCompensate(TState state);
}

public interface ISaga<TState, TCommand, TResult>
    where TState : ISagaState
{
    string SagaName { get; }
    Task<SagaResult<TResult>> ExecuteAsync(TCommand command, CancellationToken ct);
    Task<SagaResult> ResumeAsync(TState state, CancellationToken ct);
    Task<SagaResult> CompensateAsync(TState state, CancellationToken ct);
}


public interface ISagaStateRepository
{
    Task<TState?> GetByIdAsync<TState>(Guid sagaId, CancellationToken ct = default) where TState : class, ISagaState;
    Task<IEnumerable<TState>> GetIncompleteSagasAsync<TState>(CancellationToken ct = default) where TState : class, ISagaState;
    Task<IEnumerable<TState>> GetTimedOutSagasAsync<TState>(CancellationToken ct = default) where TState : class, ISagaState;
    Task SaveAsync<TState>(TState state, CancellationToken ct = default) where TState : class, ISagaState;
    Task UpdateAsync<TState>(TState state, CancellationToken ct = default) where TState : class, ISagaState;
}


