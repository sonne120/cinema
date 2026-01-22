using System.Text.Json;
using Cinema.Application.Sagas;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence.Write;

public class SagaStateEntity
{
    public Guid Id { get; set; }
    public string SagaType { get; set; } = string.Empty;
    public int Status { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string SerializedData { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
