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
    /// The event arguments
    /// </summary>
    public FuncArg[] Args { get; private set; } = [];
    
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
        Return = @event?.Return ?? string.Empty;
        Args = @event?.Args ?? [];
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
        if (!type.IsAssignableTo(typeof(Event<>))) return [];
        
        // Common
        SchemaTypeAttribute? typeAttr = type.GetCustomAttribute<SchemaTypeAttribute>();
        string typeName = typeAttr?.Name ?? $"{(string.IsNullOrWhiteSpace(ns) ? "" : $"{ns}.")}{type.Name.ToLowerInvariant()}";
        NodeSchema eventSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Event,
            Display = typeAttr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Event = new EventSchema
            {
                Scope = EventScope.Workflow,
                Return = "",
                Args = [],
            }
        };
        
        // Arguments
        ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (constructors.Length > 0)
        {
            // only use the first constructor
            ConstructorInfo constructor = constructors[0];
            ParameterInfo[] parameters = constructor.GetParameters();
            eventSchema.Event.Args = new FuncArg[parameters.Length];
            for(int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                SchemaTypeAttribute? paramAttr = parameter.GetCustomAttribute<SchemaTypeAttribute>();
                eventSchema.Event.Args[i] = new FuncArg
                {
                    Name = parameter.Name ?? $"arg{i}",
                    Type = paramAttr?.Name ?? parameter.ParameterType.GetSchemaType(true) ?? "T",
                };
            }
        }

        // Scope & Return Type
        Type? checkType = type;
        while (checkType != null)
        {
            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(WorkflowEvent<>))
                {
                    Type dataType = type.GetGenericArguments()[0];
                    eventSchema.Event.Return = dataType.GetSchemaType(true) ?? "T";
                    eventSchema.Event.Scope = EventScope.Workflow;
                    break;
                }
                else if (genericDef == typeof(ApplicationEvent<>))
                {
                    Type dataType = type.GetGenericArguments()[0];
                    eventSchema.Event.Return = dataType.GetSchemaType(true) ?? "T";
                    eventSchema.Event.Scope = EventScope.Application;
                }
                else if (genericDef == typeof(ServerEvent<>))
                {
                    Type dataType = type.GetGenericArguments()[0];
                    eventSchema.Event.Return = dataType.GetSchemaType(true) ?? "T";
                    eventSchema.Event.Scope = EventScope.Server;
                    break;
                }
                else if (genericDef == typeof(ClusterEvent<>))
                {
                    Type dataType = type.GetGenericArguments()[0];
                    eventSchema.Event.Return = dataType.GetSchemaType(true) ?? "T";
                    eventSchema.Event.Scope = EventScope.Cluster;
                    break;
                }
            }
            checkType = checkType.BaseType;
        }
        if (checkType == null) return [];
        
        EventTypeNames[type] = typeName;
        
        return [ eventSchema ];
    }

    /// <summary>
    /// Gets the schema event name for the given event type
    /// </summary>
    public static string? GetEventName<T>(T obj)
    {
        return EventTypeNames.GetValueOrDefault(typeof(T));
    }
    
    /// <summary>
    /// Gets the schema event name for the given event type
    /// </summary>
    public static string? GetEventName(Type type)
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
                Return = schema.Return,
                Args = schema.Args.ToArray(),
                Additional = schema.Additional,
            }
        };
    }
     
    #endregion
}