using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.Infrastructure.ServiceDiscovery;

public class ConsulServiceRegistration : IHostedService
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulSettings _settings;
    private readonly ILogger<ConsulServiceRegistration> _logger;
    private readonly string _registrationId;

    public ConsulServiceRegistration(
        IConsulClient consulClient,
        IOptions<ConsulSettings> settings,
        ILogger<ConsulServiceRegistration> logger)
    {
        _consulClient = consulClient;
        _settings = settings.Value;
        _logger = logger;
        _registrationId = string.IsNullOrEmpty(_settings.ServiceId)
            ? $"{_settings.ServiceName}-{Guid.NewGuid():N}"
            : _settings.ServiceId;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Registering service {ServiceName} with Consul at {ConsulAddress}",
            _settings.ServiceName,
            _settings.Address);

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = _settings.ServiceName,
            Address = _settings.ServiceHost,
            Port = _settings.ServicePort,
            Tags = _settings.Tags,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_settings.ServiceHost}:{_settings.ServicePort}/health",
                Interval = TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds),
                Timeout = TimeSpan.FromSeconds(_settings.HealthCheckTimeoutSeconds),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(_settings.DeregisterCriticalServiceAfterMinutes)
            }
        };

        try
        {
            await _consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
            await _consulClient.Agent.ServiceRegister(registration, cancellationToken);

            _logger.LogInformation(
                "Service {ServiceName} registered with ID {ServiceId}",
                _settings.ServiceName,
                _registrationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering service {ServiceName} with Consul", _settings.ServiceName);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deregistering service {ServiceId} from Consul", _registrationId);

        try
        {
            await _consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
            _logger.LogInformation("Service {ServiceId} deregistered from Consul", _registrationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deregistering service {ServiceId} from Consul", _registrationId);
        }
    }
}
