using Cinema.Application.Common.Interfaces.Authentication;
using Cinema.Application.Common.Interfaces.Messaging;
using Cinema.Application.Common.Interfaces.Persistence;
using Cinema.Application.Common.Interfaces.Queries;
using Cinema.Application.Common.Interfaces.Services;
using Cinema.Application.Sagas;
using Cinema.Application.Sagas.TicketPurchase;
using Cinema.Application.Sagas.TicketPurchase.Steps;
using Cinema.Domain.AuditoriumAggregate;
using Cinema.Domain.PaymentAggregate;
using Cinema.Domain.TicketAggregate;
using Cinema.Domain.UserAggregate;
using Cinema.Infrastructure.Authentication;
using Cinema.Infrastructure.BackgroundJobs;
using Cinema.Infrastructure.Messaging;
using Cinema.Infrastructure.Persistence.Read;
using Cinema.Infrastructure.Persistence.Read.Repositories;
using Cinema.Infrastructure.Persistence.Write;
using Cinema.Infrastructure.Persistence.Write.Interceptors;
using Cinema.Infrastructure.Services;
using Cinema.Infrastructure.Persistence.Write.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Cinema.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isWriteSide = true)
    {
        services
            .AddPersistence(configuration)
            .AddMessaging(configuration)
            .AddAuth(configuration);

        if (isWriteSide)
        {
            services.AddBackgroundJobs();
        }

        return services;
    }

    private static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IAuthService, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        
        services.AddSingleton<PublishDomainEventsInterceptor>();

        services.AddDbContext<CinemaDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<PublishDomainEventsInterceptor>();
            
            options.UseSqlServer(
                configuration.GetConnectionString("SqlServer"),
                b => b.MigrationsAssembly(typeof(CinemaDbContext).Assembly.FullName))
                .AddInterceptors(interceptor);
        });

        
        services.AddScoped<IShowtimeRepository, ShowtimeRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAuditoriumRepository, AuditoriumRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISagaStateRepository, SagaStateRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IEventBus, EventBus>();
        services.AddScoped<TicketPurchaseSaga>();
        services.AddScoped<ReserveSeatsStep>();
        services.AddScoped<ProcessPaymentStep>();
        services.AddScoped<ConfirmReservationStep>();
        services.AddScoped<IssueTicketStep>();
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddScoped<IAuditoriumService, AuditoriumService>();
        services.AddScoped<INotificationService, MockNotificationService>();

        
        services.Configure<MongoDbSettings>(
            configuration.GetSection(nameof(MongoDbSettings)));

        services.AddSingleton<MongoDbContext>();

        
        services.AddScoped<IShowtimeReadRepository, ShowtimeReadRepository>();
        services.AddScoped<IReservationReadRepository, ReservationReadRepository>();

        
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        
        services.AddScoped<IMovieApiService, MovieApiService>();

        return services;
    }

    private static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        
        services.Configure<KafkaSettings>(
            configuration.GetSection(nameof(KafkaSettings)));

        services.AddSingleton<IEventBus, KafkaEventBus>();
        

        return services;
    }

    private static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services)
    {
        
        services.AddQuartz(configure =>
        {
            var processOutboxJobKey = new JobKey(nameof(ProcessOutboxMessagesJob));

            configure
                .AddJob<ProcessOutboxMessagesJob>(processOutboxJobKey)
                .AddTrigger(trigger =>
                    trigger.ForJob(processOutboxJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInSeconds(10)
                                .RepeatForever()));
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
