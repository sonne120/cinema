using Consul;
using Microsoft.Extensions.Logging;

namespace Cinema.Infrastructure.ServiceDiscovery;

public interface IServiceDiscovery
{
    Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<ServiceInstance?> GetServiceInstanceAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<Uri?> GetServiceUriAsync(string serviceName, CancellationToken cancellationToken = default);
}

public record ServiceInstance(
    string ServiceId,
    string ServiceName,
    string Address,
    int Port,
    string[] Tags,
    bool IsHealthy)
{
    public Uri Uri => new($"http://{Address}:{Port}");
}

public class ConsulServiceDiscovery : IServiceDiscovery
{
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ConsulServiceDiscovery> _logger;
    private readonly Random _random = new();

    public ConsulServiceDiscovery(
        IConsulClient consulClient,
        ILogger<ConsulServiceDiscovery> logger)
    {
        _consulClient = consulClient;
        _logger = logger;
    }

    public async Task<IEnumerable<ServiceInstance>> GetServiceInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var services = await _consulClient.Health.Service(serviceName, string.Empty, passingOnly: true, cancellationToken);

            var instances = services.Response.Select(s => new ServiceInstance(
                s.Service.ID,
                s.Service.Service,
                s.Service.Address,
                s.Service.Port,
                s.Service.Tags,
                s.Checks.All(c => c.Status == HealthStatus.Passing)
            )).ToList();

            _logger.LogDebug("Found {Count} healthy instances for service {ServiceName}", instances.Count, serviceName);

            return instances;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering service {ServiceName}", serviceName);
            return Enumerable.Empty<ServiceInstance>();
        }
    }

    public async Task<ServiceInstance?> GetServiceInstanceAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var instances = (await GetServiceInstancesAsync(serviceName, cancellationToken)).ToList();

        if (instances.Count == 0)
        {
            _logger.LogWarning("No healthy instances found for service {ServiceName}", serviceName);
            return null;
        }

        var index = _random.Next(instances.Count);
        return instances[index];
    }

    public async Task<Uri?> GetServiceUriAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var instance = await GetServiceInstanceAsync(serviceName, cancellationToken);
        return instance?.Uri;
    }
}
