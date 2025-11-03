using System.Text.Json;
using System.Text.Json.Serialization;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;

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
    public Event Event { get; private set; } = Event.Workflow;
    
    /// <summary>
    /// The event value type
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
        Event = @event?.Event ?? Enum.Event.Workflow;
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

    public static NodeSchema[] GenerateSystemStruct(Type type, string? ns = null)
    {
        return [];
    }
    
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
                Event = schema.Event,
                Return = schema.Return,
                Args = schema.Args.ToArray(),
                Additional = schema.Additional,
            }
        };
    }
     
    #endregion
}