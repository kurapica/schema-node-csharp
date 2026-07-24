using SchemaNode.Context;
using SchemaNode.Event;
using SchemaNode.Property.Event;
using SchemaNode.Relation;
using SchemaNode.Schema;
using SchemaNode.Utility;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory event schema representation
/// </summary>
public sealed class EventType: NodeType
{
    #region Fields

    private EventSchema? _eventSchema;

    #endregion
    
    #region Properties
    
    /// <summary>
    /// The payload type
    /// </summary>
    public ValueType? Payload { get; private set; }
    
    /// <summary>
    /// The payload evaluator
    /// </summary>
    public FunctionType? PayloadEvaluator { get; private set; }
    
    /// <summary>
    /// The event argument
    /// </summary>
    internal FuncArg[]? Args => _eventSchema?.Args;

    /// <inheritdoc />
    public override bool IsUsed => true;

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
            Payload = await context.GetNodeTypeAsync<ValueType>(_eventSchema.Payload, Generics, GenericParams);
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
    public override void Unload()
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
    
    /// <summary>
    /// Gets the event instance with arguments
    /// </summary>
    public async Task<BaseEvent?> GetEventInstance(SchemaContext context, object?[]? args = null)
    {
        var type = GetCsharpType();
        if (type == null) return null;
        if (Args == null || Args.Length == 0) return Activator.CreateInstance(type) as BaseEvent;
        
        object?[] genArgs = new object?[Args.Length];
        for (int i = 0; i < Args.Length; i++)
        {
            var arg = Args[i];
            var argType = (await context.GetNodeTypeAsync<ValueType>(arg.Type))?.GetCsharpType();
            if (argType == null) return null; // keep simple
            genArgs[i] = argType.TryConvert(args?.ElementAtOrDefault(i), out var result)  ? result : null;
        }
        return Activator.CreateInstance(type, genArgs) as BaseEvent;
    }

    /// <summary>
    /// Gets the payload type
    /// </summary>
    public async Task<ValueType?> GetPayloadType(SchemaContext context, object[]? args = null)
    {
        // Calc the payload type with the payload evaluator if existed
        if (PayloadEvaluator != null)
        {
            try
            {
                string? type = await PayloadEvaluator.CallAsync<string>(context, args ?? []);
                if (type != null)
                    return await context.GetNodeTypeAsync<ValueType>(type);
            }
            catch
            {
                context.LogError($"Failed to evaluate payload type for event {Name} with evaluator {PayloadEvaluator.Name}");
                throw;
            }
        }
        return Payload;
    }

    #endregion
}