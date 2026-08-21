using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.ServiceDiscovery;

public static class ConsulExtensions
{
    public static IServiceCollection AddConsulServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConsulSettings>(configuration.GetSection(ConsulSettings.SectionName));

        var consulSettings = configuration.GetSection(ConsulSettings.SectionName).Get<ConsulSettings>()
            ?? new ConsulSettings();

        services.AddSingleton<IConsulClient>(_ => new ConsulClient(config =>
        {
            config.Address = new Uri(consulSettings.Address);
        }));

        services.AddSingleton<IServiceDiscovery, ConsulServiceDiscovery>();

        return services;
    }

    public static IServiceCollection AddConsulServiceRegistration(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string serviceHost,
        int servicePort,
        string[]? tags = null,
        ConsulCheckType checkType = ConsulCheckType.Http,
        string healthCheckPath = "/health")
    {
        services.Configure<ConsulSettings>(options =>
        {
            var consulSection = configuration.GetSection(ConsulSettings.SectionName);
            consulSection.Bind(options);

            options.ServiceName = serviceName;
            options.ServiceHost = serviceHost;
            options.ServicePort = servicePort;
            options.Tags = tags ?? new[] { "api", "dotnet" };
            options.CheckType = checkType;
            options.HealthCheckPath = healthCheckPath;

            // A stable id keyed on the instance host means a restarting container
            // replaces its own registration instead of leaving a critical orphan
            // behind for DeregisterCriticalServiceAfter to reap.
            if (string.IsNullOrEmpty(options.ServiceId))
            {
                options.ServiceId = $"{serviceName}-{serviceHost}";
            }
        });

        var consulSettings = configuration.GetSection(ConsulSettings.SectionName).Get<ConsulSettings>()
            ?? new ConsulSettings();

        services.AddSingleton<IConsulClient>(_ => new ConsulClient(config =>
        {
            config.Address = new Uri(consulSettings.Address);
        }));

        services.AddSingleton<IServiceDiscovery, ConsulServiceDiscovery>();
        services.AddHostedService<ConsulServiceRegistration>();

        return services;
    }
}
