using Consul;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Cinema.LoadBalancer;

public class ConsulProxyConfigProvider : IProxyConfigProvider, IDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ConsulProxyConfigProvider> _logger;
    private readonly string _serviceName;
    private readonly CancellationTokenSource _cts = new();
    private volatile ConsulProxyConfig _config;
    private readonly object _lock = new();

    public ConsulProxyConfigProvider(
        IConsulClient consulClient,
        ILogger<ConsulProxyConfigProvider> logger,
        IConfiguration configuration)
    {
        _consulClient = consulClient;
        _logger = logger;
        // Allows balancing different services, defaults to main API
        _serviceName = configuration["Consul:TargetServiceName"] ?? "cinema-api";
        _config = new ConsulProxyConfig(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());

        // Start background polling
        _ = Task.Run(PollConsulAsync);
    }

    public IProxyConfig GetConfig() => _config;

    private async Task PollConsulAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await UpdateConfigAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Consul for service {ServiceName}", _serviceName);
            }

            // Poll interval
            await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
        }
    }

    private async Task UpdateConfigAsync()
    {
        // Get all healthy instances of the service
        var services = await _consulClient.Health.Service(_serviceName, tag: null, passingOnly: true, _cts.Token);
        
        var destinations = new Dictionary<string, DestinationConfig>();

        foreach (var s in services.Response)
        {
            // IMPORTANT: In K8s, we need the POD IP.
            var address = !string.IsNullOrEmpty(s.Service.Address) ? s.Service.Address : s.Node.Address;
            var port = s.Service.Port;
            
            destinations.Add(s.Service.ID, new DestinationConfig
            {
                Address = $"http://{address}:{port}"
            });
        }

        if (destinations.Count == 0 && _config.Clusters.Any())
        {
            _logger.LogWarning("No healthy instances found for service {ServiceName}. Keeping previous config.", _serviceName);
            // Optional: Strategy to keep old config or clear it. Here we protect against total failure.
            return;
        }

        if (destinations.Count != _config.Clusters.FirstOrDefault()?.Destinations?.Count)
        {
             _logger.LogInformation("Updating config. Found {Count} instances for {ServiceName}", destinations.Count, _serviceName);
             
             // Define the Cluster (The backends)
             var cluster = new ClusterConfig
             {
                 ClusterId = "cinema-api-cluster",
                 LoadBalancingPolicy = "RoundRobin",
                 Destinations = destinations,
                 HealthCheck = new HealthCheckConfig
                 {
                     Active = new ActiveHealthCheckConfig
                     {
                         Enabled = true,
                         Interval = TimeSpan.FromSeconds(10),
                         Timeout = TimeSpan.FromSeconds(5),
                         Path = "/health",
                         Policy = "ConsecutiveFailures",
                     }
                 },
                 HttpRequest = new ForwarderRequestConfig
                 {
                     ActivityTimeout = TimeSpan.FromSeconds(60),
                     Version = Version.Parse("2.0"),
                     VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                 }
             };

             // Define the Route (The matching rule)
             var route = new RouteConfig
             {
                 RouteId = "cinema-api-catchall",
                 ClusterId = "cinema-api-cluster",
                 Match = new RouteMatch
                 {
                     Path = "{**catch-all}"
                 }
             };

             var oldConfig = _config;
             _config = new ConsulProxyConfig(new[] { route }, new[] { cluster });
             
             // Signal that config has changed
             oldConfig.SignalChange();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

// Config Helper Class
public class ConsulProxyConfig : IProxyConfig
{
    private readonly CancellationTokenSource _cts = new();

    public ConsulProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    public void SignalChange()
    {
        _cts.Cancel();
    }
}
