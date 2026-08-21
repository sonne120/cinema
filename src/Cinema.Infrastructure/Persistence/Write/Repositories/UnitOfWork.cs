using Cinema.Application.Common.Interfaces.Persistence;
using Cinema.Domain.Common.Models;
using Cinema.Infrastructure.Persistence.Write.Outbox;
using Newtonsoft.Json;

namespace Cinema.Infrastructure.Persistence.Write.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly CinemaDbContext _dbContext;

    public UnitOfWork(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertDomainEventsToOutboxMessages();
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ConvertDomainEventsToOutboxMessages()
    {
        var outboxMessages = _dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(x => x.Entity)
            .SelectMany(aggregate =>
            {
                var domainEvents = aggregate.DomainEvents.ToList();
                aggregate.ClearDomainEvents();
                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = DateTime.UtcNow,
                Type = domainEvent.GetType().Name,
                Content = JsonConvert.SerializeObject(
                    domainEvent,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    })
            })
            .ToList();

        if (outboxMessages.Any())
        {
            _dbContext.Set<OutboxMessage>().AddRange(outboxMessages);
        }
    }
}
