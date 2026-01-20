using System.Text.Json;
using Cinema.Application.Common.Interfaces.Messaging;
using Cinema.Domain.Common.Models;
using Cinema.Infrastructure.Persistence.Write;
using Cinema.Infrastructure.Persistence.Write.Outbox;

namespace Cinema.Infrastructure.Messaging;

public class EventBus : IEventBus
{
    private readonly CinemaDbContext _context;

    public EventBus(CinemaDbContext context)
    {
        _context = context;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = @event.GetType().Name,
            Content = JsonSerializer.Serialize(@event, @event.GetType()),
            OccurredOnUtc = @event.OccurredOnUtc
        };

        await _context.OutboxMessages.AddAsync(outboxMessage, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        var outboxMessages = events.Select(e => new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = e.GetType().Name,
            Content = JsonSerializer.Serialize(e, e.GetType()),
            OccurredOnUtc = e.OccurredOnUtc
        });

        await _context.OutboxMessages.AddRangeAsync(outboxMessages, ct);
        await _context.SaveChangesAsync(ct);
    }
}
