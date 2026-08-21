using Cinema.Application.Common.Interfaces.Queries;
using Cinema.Persistence.Read.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Persistence.Read;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the MongoDB read models and their repositories.
    /// </summary>
    public static IServiceCollection AddMongoReadPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = MongoDbSettings.SectionName)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection(sectionName));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IShowtimeReadRepository, ShowtimeReadRepository>();
        services.AddScoped<IReservationReadRepository, ReservationReadRepository>();

        return services;
    }
}
