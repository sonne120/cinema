using Cinema.ReadService.Messaging;
using Cinema.Persistence.Read;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.ReadService;

public static class DependencyInjection
{
    public static IServiceCollection AddReadService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoReadPersistence(configuration);

        services.Configure<KafkaSettings>(
            configuration.GetSection(KafkaSettings.SectionName));
        services.AddHostedService<KafkaConsumer>();

        return services;
    }
}
