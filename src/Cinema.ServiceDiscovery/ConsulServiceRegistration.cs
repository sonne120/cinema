using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.ServiceDiscovery;

/// <summary>
/// Keeps this instance registered in Consul for as long as the process lives.
///
/// Registration is re-asserted on a timer rather than done once at startup: Consul
/// may be unreachable when we boot, and a Consul restart drops every registration it
/// was holding. A one-shot registration leaves the catalog silently empty in both
/// cases, with no way back short of restarting every service.
/// </summary>
public class ConsulServiceRegistration : IHostedService, IAsyncDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulSettings _settings;
    private readonly ILogger<ConsulServiceRegistration> _logger;
    private readonly string _registrationId;
    private readonly string _ttlCheckId;
    private readonly TimeSpan _period;
    private Timer? _timer;
    private bool _registered;

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
        _ttlCheckId = $"service:{_registrationId}";
        _period = TimeSpan.FromSeconds(Math.Max(1, _settings.HealthCheckIntervalSeconds));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Maintaining Consul registration for {ServiceName} ({CheckType} check) at {ConsulAddress}",
            _settings.ServiceName,
            _settings.CheckType,
            _settings.Address);

        _timer = new Timer(_ => _ = SyncAsync(), state: null, dueTime: TimeSpan.Zero, period: _period);
        return Task.CompletedTask;
    }

    private async Task SyncAsync()
    {
        try
        {
            var services = await _consulClient.Agent.Services();
            var present = services.Response is not null
                          && services.Response.ContainsKey(_registrationId);

            if (!present)
            {
                await _consulClient.Agent.ServiceRegister(BuildRegistration());
                _logger.LogInformation(
                    "Service {ServiceName} registered with ID {ServiceId}",
                    _settings.ServiceName,
                    _registrationId);
                _registered = true;
            }

            // TTL must be reported after (re-)registration — a fresh TTL check starts
            // critical and only becomes passing once the first heartbeat lands.
            if (_settings.CheckType == ConsulCheckType.Ttl)
            {
                await _consulClient.Agent.PassTTL(_ttlCheckId, "heartbeat");
            }
        }
        catch (Exception ex)
        {
            // Consul being briefly unavailable is expected during a restart; the next
            // tick retries, so this stays a warning rather than an error.
            _logger.LogWarning(
                ex,
                "Could not sync Consul registration for {ServiceName}; retrying in {Period}s",
                _settings.ServiceName,
                _period.TotalSeconds);
        }
    }

    private AgentServiceRegistration BuildRegistration() => new()
    {
        ID = _registrationId,
        Name = _settings.ServiceName,
        Address = _settings.ServiceHost,
        Port = _settings.ServicePort,
        Tags = _settings.Tags,
        Check = BuildCheck()
    };

    private AgentServiceCheck BuildCheck()
    {
        var interval = TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds);
        var timeout = TimeSpan.FromSeconds(_settings.HealthCheckTimeoutSeconds);
        var deregisterAfter = TimeSpan.FromMinutes(_settings.DeregisterCriticalServiceAfterMinutes);

        return _settings.CheckType switch
        {
            // A TCP connect is all we can do against a listener that does not speak
            // HTTP/1.1 — Consul's HTTP check would be rejected by an h2c-only Kestrel.
            ConsulCheckType.Tcp => new AgentServiceCheck
            {
                TCP = $"{_settings.ServiceHost}:{_settings.ServicePort}",
                Interval = interval,
                Timeout = timeout,
                DeregisterCriticalServiceAfter = deregisterAfter
            },

            // No socket to probe: the process reports its own liveness instead. The TTL
            // is generous relative to the heartbeat so one slow tick does not flap it.
            ConsulCheckType.Ttl => new AgentServiceCheck
            {
                TTL = TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds * 3),
                DeregisterCriticalServiceAfter = deregisterAfter
            },

            _ => new AgentServiceCheck
            {
                HTTP = $"http://{_settings.ServiceHost}:{_settings.ServicePort}{_settings.HealthCheckPath}",
                Interval = interval,
                Timeout = timeout,
                DeregisterCriticalServiceAfter = deregisterAfter
            }
        };
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopTimerAsync();

        if (!_registered)
        {
            return;
        }

        _logger.LogInformation("Deregistering service {ServiceId} from Consul", _registrationId);

        try
        {
            await _consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deregistering service {ServiceId} from Consul", _registrationId);
        }
    }

    private async ValueTask StopTimerAsync()
    {
        if (_timer is not null)
        {
            await _timer.DisposeAsync();
            _timer = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopTimerAsync();
        GC.SuppressFinalize(this);
    }
}
