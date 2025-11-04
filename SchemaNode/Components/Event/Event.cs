using SchemaNode.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.Components;

/// <summary>
/// The base event
/// </summary>
public abstract class Event<T>
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
    /// The event scope
    /// </summary>
    public abstract EventScope Scope { get; }
    
    /// <summary>
    /// The topic name
    /// </summary>
    public virtual string Topic
    {
        set => _topic = value;
        get {
            if (string.IsNullOrEmpty(_topic))
                _topic = EventType.GetEventName(this)!.Replace(".", "_").ToLower();
            return _topic;
        }
    }
    
    /// <summary>
    /// The event data
    /// </summary>
    public T? Payload { get; set; }
}
