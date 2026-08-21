namespace Cinema.ServiceDiscovery;

/// <summary>
/// How Consul should health-check a registered instance. Not every service can
/// answer a plain HTTP GET, so the check has to match the service's shape.
/// </summary>
public enum ConsulCheckType
{
    /// <summary>HTTP GET against <see cref="ConsulSettings.HealthCheckPath"/>. Default.</summary>
    Http,

    /// <summary>TCP connect only — for listeners that speak something other than HTTP/1.1 (e.g. h2c gRPC).</summary>
    Tcp,

    /// <summary>Application pushes a heartbeat. For workers with no listening socket.</summary>
    Ttl
}

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

    public ConsulCheckType CheckType { get; set; } = ConsulCheckType.Http;
    public string HealthCheckPath { get; set; } = "/health";

    public string Address => $"http://{Host}:{Port}";
}
