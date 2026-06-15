using SchemaNode.Context;
using SchemaNode.Property.Event;
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
    
    /// <summary>
    /// The payload evaluator
    /// </summary>
    public FunctionType? PayloadEvaluator { get; private set; }

    /// <inheritdoc />
    public override bool IsUsed => true;

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        EventSchema? eventSchema = GetProperty<EventProperty>()?.Value;
        if (eventSchema == null)
            Error = ErrorCodes.NO_DEFINITION;

        if (!string.IsNullOrWhiteSpace(eventSchema?.Payload))
        {
            Payload = await context.GetNodeTypeAsync<ValueType>(eventSchema.Payload, Generics, GenericParams);
            if (Payload == null)
                Error ??= AppErrorCodes.EVENT_PAYLOAD_NOT_VALID;
        }

        string? payloadEvaluator = GetProperty<PayloadEvaluator>()?.Value;
        if (!string.IsNullOrWhiteSpace(payloadEvaluator))
        {
            PayloadEvaluator = await context.GetNodeTypeAsync<FunctionType>(payloadEvaluator);
            if (PayloadEvaluator == null)
                Error ??= AppErrorCodes.EVENT_PAYLOAD_NOT_VALID;
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        Payload = null;
        PayloadEvaluator = null;
    }

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Payload != null)
            yield return Payload;
        
        if (PayloadEvaluator != null)
            yield return PayloadEvaluator;

        foreach (var t in base.GetReferenceTypes())
            yield return t;
    }

    #endregion
}