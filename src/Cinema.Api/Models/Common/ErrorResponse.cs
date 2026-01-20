namespace Cinema.Api.Models.Common;

public class ErrorResponse
{
    public string Error { get; set; }
    public Guid? SagaId { get; set; }

    public ErrorResponse(string error, Guid? sagaId = null)
    {
        Error = error;
        SagaId = sagaId;
    }
}
