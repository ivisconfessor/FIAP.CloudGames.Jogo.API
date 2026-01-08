using System.Text.Json;

namespace FIAP.CloudGames.Jogo.API.Infrastructure.EventSourcing;

public interface IEventStore
{
    Task SaveEventAsync<TEvent>(TEvent @event) where TEvent : class;
    Task<IEnumerable<StoredEvent>> GetEventsAsync(Guid aggregateId);
}

public class EventStore : IEventStore
{
    private readonly ILogger<EventStore> _logger;
    private static readonly List<StoredEvent> _events = new();

    public EventStore(ILogger<EventStore> logger)
    {
        _logger = logger;
    }

    public Task SaveEventAsync<TEvent>(TEvent @event) where TEvent : class
    {
        var eventType = @event.GetType().Name;
        var eventData = JsonSerializer.Serialize(@event);
        
        var storedEvent = new StoredEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            EventData = eventData,
            Timestamp = DateTime.UtcNow
        };

        _events.Add(storedEvent);
        
        _logger.LogInformation("Event {EventType} saved at {Timestamp}", eventType, storedEvent.Timestamp);
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<StoredEvent>> GetEventsAsync(Guid aggregateId)
    {
        var events = _events.Where(e => e.EventData.Contains(aggregateId.ToString()));
        return Task.FromResult(events);
    }
}

public class StoredEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
