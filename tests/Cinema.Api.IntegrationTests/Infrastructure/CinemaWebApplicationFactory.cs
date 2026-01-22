using Cinema.Application.Common.Interfaces.Messaging;
using Cinema.Domain.Common.Models;
using Cinema.Infrastructure.Persistence.Write;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cinema.Api.IntegrationTests.Infrastructure;

public class CinemaWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly string _databaseName = $"CinemaTestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CinemaDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove existing DbContext
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(CinemaDbContext));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Remove Kafka event bus and replace with in-memory implementation
            services.RemoveAll<IEventBus>();
            services.AddScoped<IEventBus, InMemoryEventBus>();

            // Remove Quartz hosted services
            var quartzServices = services.Where(
                d => d.ServiceType == typeof(IHostedService) &&
                     d.ImplementationType?.FullName?.Contains("Quartz") == true).ToList();
            foreach (var service in quartzServices)
            {
                services.Remove(service);
            }

            // Also remove any background job services that depend on Quartz
            var backgroundJobServices = services.Where(
                d => d.ServiceType == typeof(IHostedService) &&
                     d.ImplementationType?.FullName?.Contains("Background") == true).ToList();
            foreach (var service in backgroundJobServices)
            {
                services.Remove(service);
            }

            // Add in-memory database
            services.AddDbContext<CinemaDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Build service provider
            var sp = services.BuildServiceProvider();

            // Create a scope and ensure database is created
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all Quartz-related hosted services
            services.RemoveAll<Quartz.ISchedulerFactory>();
            services.RemoveAll<Quartz.IScheduler>();
        });

        return base.CreateHost(builder);
    }
}

public class InMemoryEventBus : IEventBus
{
    private readonly List<IDomainEvent> _publishedEvents = new();

    public IReadOnlyList<IDomainEvent> PublishedEvents => _publishedEvents.AsReadOnly();

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IDomainEvent
    {
        _publishedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        _publishedEvents.AddRange(events);
        return Task.CompletedTask;
    }
}

