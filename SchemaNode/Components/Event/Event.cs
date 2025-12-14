using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedTypeParameter

namespace SchemaNode.Components;

/// <summary>
/// The base event
/// </summary>
public abstract class Event
{
    /// <summary>
    /// The event identifier
    /// </summary>
    public Guid Id { get; } = Guid.CreateVersion7();

    /// <summary>
    /// The event timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The event topic name like server/topic/action/guid
    /// So they can be subscribed by wildcard topic, + for one, *,# for multi
    /// </summary>
    public virtual string Topic => string.Empty;

    /// <summary>
    /// The generic payload data
    /// </summary>
    public AnySchemaNode? Payload { get; set; }

    /// <summary>
    /// Match the topic with wildcard support
    /// </summary>
    public bool MatchTopic(string topic)
    {
        if (string.IsNullOrEmpty(Topic) || Topic == "*") return true; // all match
        if (string.IsNullOrEmpty(topic)) return false;

        if (Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)) return true;

        // match with wildcard
        string[] topicParts = Topic.Split(TOPIC_SEP, StringSplitOptions.RemoveEmptyEntries);
        string[] matchParts = topic.Split(TOPIC_SEP, StringSplitOptions.RemoveEmptyEntries);
        if (matchParts.Length > topicParts.Length) return false;

        for (int i = 0; i < matchParts.Length; i++)
        {
            if (matchParts[i] == TOPIC_WILDCARD_SINGLE) continue; // match single part
            if (matchParts[i] == TOPIC_WILDCARD_MULTI || matchParts[i] == TOPIC_WILDCARD_ALL) return true; // match all remaining parts

            if (!topicParts[i].Equals(matchParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    } 
}

/// <summary>
/// The event has generic payload, determined by usage
/// </summary>
public interface IEventPayload
{
}

/// <summary>
/// The event with given type payload
/// </summary>
public interface IEventPayload<T>
{
}
