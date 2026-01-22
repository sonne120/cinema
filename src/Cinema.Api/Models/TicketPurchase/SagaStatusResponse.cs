namespace Cinema.Api.Models.TicketPurchase;

public class SagaStatusResponse
{
    public Guid SagaId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public List<StepLogDto> StepLogs { get; set; } = new();
}

public class StepLogDto
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
