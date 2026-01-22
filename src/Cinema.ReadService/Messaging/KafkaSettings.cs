namespace Cinema.ReadService.Messaging;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string DomainEventsTopic { get; set; } = "cinema.domain.events";
    public string ConsumerGroupId { get; set; } = "cinema-read-consumer-group";
}
