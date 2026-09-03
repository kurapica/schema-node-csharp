using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedTypeParameter

namespace SchemaNode.Event;

/// <summary>
/// The base event
/// </summary>
public abstract class BaseEvent
{
    /// <summary>
    /// The event identifier
    /// </summary>
    [SchemaIgnore]
    public Guid Id { get; } = Guid.CreateVersion7();

    /// <summary>
    /// The event timestamp
    /// </summary>
    [SchemaIgnore]
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The event topic name like server/topic/action/guid
    /// So they can be subscribed by wildcard topic, + for one, *,# for multi
    /// </summary>
    [SchemaIgnore]
    public virtual string Topic => string.Empty;
    
    /// <summary>
    /// The matching topic based on the event argument, used for workflow subscribe event with wildcards
    /// </summary>
    [SchemaIgnore]
    public virtual string MatchTopic => "#";
    
    /// <summary>
    /// The generic payload data
    /// </summary>
    [SchemaIgnore]
    public IValueAccess? Payload { get; set; }

    /// <summary>
    /// Match the topic with wildcard support
    /// </summary>
    public bool IsTopicMatch(string topic)
    {
        if (string.IsNullOrEmpty(Topic) || Topic == "*") return true; // all contains
        if (string.IsNullOrEmpty(topic)) return false;

        if (Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)) return true;

        // contains with wildcard
        string[] topicParts = Topic.Split(TOPIC_SEP, StringSplitOptions.RemoveEmptyEntries);
        string[] matchParts = topic.Split(TOPIC_SEP, StringSplitOptions.RemoveEmptyEntries);
        if (matchParts.Length > topicParts.Length) return false;

        for (int i = 0; i < matchParts.Length; i++)
        {
            if (matchParts[i] == TOPIC_WILDCARD_SINGLE) continue; // contains single part
            if (matchParts[i] == TOPIC_WILDCARD_MULTI || matchParts[i] == TOPIC_WILDCARD_ALL) return true; // contains all remaining parts

            if (!topicParts[i].Equals(matchParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    } 
}

/// <summary>
/// The event has payload
/// </summary>
public interface IEventPayload<T>;