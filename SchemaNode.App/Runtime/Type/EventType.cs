using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory event schema representation
/// </summary>
public sealed class EventType: NodeType
{
    #region Properties
    
    /// <summary>
    /// The payload type
    /// </summary>
    public ValueType? Payload { get; private set; }

    /// <inheritdoc />
    public override bool IsUsed => true;

    private EventSchema? _eventSchema = null;

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        _eventSchema = GetProperty<EventProperty>()?.Value;
        if (_eventSchema == null)
            Error = ErrorCodes.NO_DEFINITION;
        
        // Payload
        Payload = !string.IsNullOrWhiteSpace(_eventSchema?.Payload) 
            ? await context.GetNodeTypeAsync<ValueType>(_eventSchema.Payload)
            : null;
    }

    #endregion
}