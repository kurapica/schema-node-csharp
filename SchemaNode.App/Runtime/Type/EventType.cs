using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Utility;
// ReSharper disable UnusedAutoPropertyAccessor.Global

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

    private EventSchema? _eventSchema;

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        _eventSchema = GetProperty<EventProperty>()?.Value;
        if (_eventSchema == null)
            Error = ErrorCodes.NO_DEFINITION;

        if (!string.IsNullOrWhiteSpace(_eventSchema?.Payload))
        {
            Payload = await context.GetNodeTypeAsync<ValueType>(_eventSchema.Payload, Generics);
            if (Payload == null)
            {
                Error = AppErrorCodes.EVENT_POLICY_NOT_VALID;
                return;
            }
        }
    }

    #endregion
}