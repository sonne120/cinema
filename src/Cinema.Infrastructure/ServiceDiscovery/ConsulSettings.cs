namespace Cinema.Infrastructure.ServiceDiscovery;

public class ConsulSettings
{
    public const string SectionName = "Consul";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8500;
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceHost { get; set; } = string.Empty;
    public int ServicePort { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int HealthCheckIntervalSeconds { get; set; } = 10;
    public int HealthCheckTimeoutSeconds { get; set; } = 5;
    public int DeregisterCriticalServiceAfterMinutes { get; set; } = 1;

    public string Address => $"http://{Host}:{Port}";
}
