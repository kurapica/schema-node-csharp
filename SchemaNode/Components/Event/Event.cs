using SchemaNode.Runtime;

namespace SchemaNode.Components;

/// <summary>
/// The base event
/// </summary>
public abstract class Event
{
    private string _topic = "";

    /// <summary>
    /// The event identifier
    /// </summary>
    public Guid Id { get; } = Guid.CreateVersion7();
    
    /// <summary>
    /// The event timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// The topic name
    /// </summary>
    public virtual string Topic
    {
        set => _topic = value;
        get {
            if (string.IsNullOrEmpty(_topic))
                _topic = EventType.GetSystemEventName(this)!.Replace(".", "_").ToLower();
            return _topic;
        }
    }
}

public interface IEventPayload<T>
{
    /// <summary>
    /// The event data
    /// </summary>
    T? Payload { get; set; }
    
    /// <summary>
    /// The origin data, only usable when the event is triggered by data change
    /// </summary>
    T? Origin { get; set; }
}