using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory event schema representation
/// </summary>
public class EventType: AnySchemeType
{
    #region Data

    /// <summary>
    /// The event type
    /// </summary>
    public EventScope Scope { get; private set; } = EventScope.Workflow;
    
    /// <summary>
    /// The default event value type, if no should be given when using
    /// </summary>
    public string Return { get; private set; } = string.Empty;
    
    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
    
    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Event;
    
    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        EventSchema? @event = schema.Event;
        
        // Data
        Scope = @event?.Scope ?? EventScope.Server;
        Return = @event?.Payload ?? string.Empty;
        Additional = @event?.Additional;

        if (@event == null) Status = SchemaNodeStatus.NoDefinition;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayNode(bool exactly = false)
    {
        return null;
    }

    #endregion
    
    #region Static Feature

    /// <summary>
    /// Generate the event schema from the given event type
    /// </summary>
    public static NodeSchema[] GenerateSystemEvent(Type type, string? ns = null)
    {
        if (!type.IsAssignableTo(typeof(Event))) return [];
        
        // Common
        SchemaTypeAttribute? typeAttr = type.GetCustomAttribute<SchemaTypeAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?.GetGenericArguments()[0];
        NodeSchema eventSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Event,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Event = new EventSchema
            {
                Scope = type.IsSubclassOf(typeof(WorkflowEvent)) 
                    ? EventScope.Workflow 
                    : type.IsSubclassOf(typeof(ApplicationEvent))
                        ? EventScope.Application
                        : type.IsSubclassOf(typeof(ServerEvent))
                            ? EventScope.Server
                            : EventScope.Cluster,
                // "T" means generic payload, choose in front-end, "" means no payload
                Payload = payloadType?.GetSchemaType(true) ?? (type.GetInterfaces().Any(i => i == typeof(IEventPayload)) ? "T" : ""),
            }
        };
        
        EventTypeNames[type] = typeName;
        
        return [ eventSchema ];
    }

    /// <summary>
    /// Gets the schema event name for the given event type
    /// </summary>
    public static string? GetSystemEventName<T>(T obj)
    {
        return EventTypeNames.GetValueOrDefault(typeof(T));
    }
    
    /// <summary>
    /// Gets the schema event name for the given event type
    /// </summary>
    public static string? GetSystemEventName(Type type)
    {
        return EventTypeNames.GetValueOrDefault(type);
    }
    
    static readonly ConcurrentDictionary<Type, string> EventTypeNames = new();
    
    #endregion
    
    #region Conversion
    
    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(EventType? schema)
    {
        if (schema == null) return null;
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            LoadState = schema.LoadState,
            Used = schema.IsUsed,
            Event = new EventSchema
            {
                Scope = schema.Scope,
                Payload = schema.Return,
                Additional = schema.Additional,
            }
        };
    }
     
    #endregion
}