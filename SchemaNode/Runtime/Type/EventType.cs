using System.Collections.Concurrent;
using System.Reflection;
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
public sealed class EventType: AnySchemaType
{
    #region Data
    
    /// <summary>
    /// The default event value type, if no should be given when using
    /// </summary>
    public string Payload { get; internal set; } = string.Empty;
    
    #endregion
    
    #region Status
    
    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Event;

    /// <inheritdoc />
    public override bool IsUsed => true;

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        EventSchema? @event = schema.Event;
        
        // Data
        Payload = @event?.Payload ?? string.Empty;

        if (@event == null) Status = SchemaNodeStatus.NoDefinition;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false)
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
        SchemaAttribute? typeAttr = type.GetCustomAttribute<SchemaAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?.GetGenericArguments()[0];

        // Keep in the same namespace if the struct is marked with SchemaAttribute, otherwise use the parent namespace
        if (typeAttr?.Name != null)
            ns = string.Join('.', typeAttr.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));

        NodeSchema eventSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Event,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Event = new EventSchema
            {
                Payload = payloadType?.GetSchemaType(true, ns) ?? (type.IsAssignableTo(typeof(IEventPayload)) ? "T" :  ""),
            }
        };

        if (Utility.SystemLocale.HasLocales)
            Utility.SystemLocale.Translate(eventSchema.Display, eventSchema.Name);

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
        return schema?.ToSchema().With(new EventSchema
        {
            Payload = schema.Payload,
        });
    }
     
    #endregion
}