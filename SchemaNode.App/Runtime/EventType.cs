using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory event schema representation.
/// Registered as the runtime type for the "event" schema kind via [Meta&lt;NodeType&gt;(typeof(EventType))] on EventSchema.
/// </summary>
public sealed class EventType : NodeType
{
    #region Properties

    /// <summary>
    /// The payload type schema name (empty string means no typed payload)
    /// </summary>
    public string Payload { get; private set; } = string.Empty;

    /// <inheritdoc />
    // Events are always considered "in use" so they are never pruned from the runtime.
    public override bool IsUsed => true;

    #endregion

    #region Overrides

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context)
    {
        EventSchema? @event = GetPropertyValue<EventSchema>();
        Payload = @event?.Payload ?? string.Empty;
        return Task.CompletedTask;
    }

    #endregion

    #region Static Event Registry

    private static readonly ConcurrentDictionary<Type, string> _eventTypeNames = new();

    /// <summary>
    /// Registers a mapping from a C# event type to its schema event name.
    /// Call this once per event type at startup or during schema generation.
    /// </summary>
    public static void RegisterEventTypeName(Type type, string name)
        => _eventTypeNames[type] = name;

    /// <summary>Gets the schema event name registered for <typeparamref name="T"/>.</summary>
    public static string? GetSystemEventName<T>() => _eventTypeNames.GetValueOrDefault(typeof(T));

    /// <summary>Gets the schema event name registered for <paramref name="type"/>.</summary>
    public static string? GetSystemEventName(Type type) => _eventTypeNames.GetValueOrDefault(type);

    #endregion
}
