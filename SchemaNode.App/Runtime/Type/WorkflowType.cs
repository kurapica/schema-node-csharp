using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory workflow schema representation
/// </summary>
public sealed class WorkflowType: NodeType
{
    #region Data

    private WorkflowSchema? _workflowSchema;
    
    /// <summary>
    /// The payload type
    /// </summary>
    public ValueType? Payload  { get; private set; }
    
    /// <summary>
    /// The state type
    /// </summary>
    public ValueType? State  { get; private set; }
    
    /// <summary>
    /// The session type
    /// </summary>
    public ValueType? Session { get; private set; }
    
    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        _workflowSchema = GetProperty<WorkflowProperty>()?.Value;
        if (_workflowSchema == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }

        if (_workflowSchema.Payload != null)
        {
            Payload = await context.GetNodeTypeAsync<ValueType>(_workflowSchema.Payload);
            if (Payload == null)
                Error ??= AppErrorCodes.WORKFLOW_PAYLOAD_NOT_VALID;
        }

        if (_workflowSchema.State != null)
        {
            State = await context.GetNodeTypeAsync<ValueType>(_workflowSchema.State);
            if (State == null)
                Error ??= AppErrorCodes.WORKFLOW_STATE_NOT_VALID;
        }

        if (_workflowSchema.Session != null)
        {
            Session = await context.GetNodeTypeAsync<ValueType>(_workflowSchema.Session);
            if (Session == null)
                Error ??= AppErrorCodes.WORKFLOW_SESSION_NOT_VALID;
        }
    }

    public override void Release()
    {
        Payload = null;
        State = null;
        Session = null;
    }

    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Payload != null)
            yield return Payload;
        if (State != null)
            yield return State;
        if (Session != null)
            yield return Session;
    }

    #endregion
}