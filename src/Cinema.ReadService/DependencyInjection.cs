using Cinema.ReadService.Messaging;
using Cinema.ReadService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.ReadService;

public static class DependencyInjection
{
    public static IServiceCollection AddReadService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(
            configuration.GetSection("MongoDB"));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IShowtimeReadRepository, ShowtimeReadRepository>();
        services.AddScoped<IReservationReadRepository, ReservationReadRepository>();

        services.Configure<KafkaSettings>(
            configuration.GetSection("Kafka"));
        services.AddHostedService<KafkaConsumer>();

        return services;
    }
}
