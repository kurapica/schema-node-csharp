using SchemaNode.Attribute;
using SchemaNode.Property.Record;

namespace SchemaNode.Utility;

internal static class WorkflowErrorCodes
{
    [Meta<ErrorCode>(EVENT_PAYLOAD_NOT_VALID)]
    public const string EVENT_PAYLOAD_NOT_VALID = "event_payload_not_valid";

    [Meta<ErrorCode>(EVENT_PAYLOAD_EVALUATOR_NOT_VALID)]
    public const string EVENT_PAYLOAD_EVALUATOR_NOT_VALID = "event_payload_evaluator_not_valid";

    [Meta<ErrorCode>(WORKFLOW_PAYLOAD_NOT_VALID)]
    public const string WORKFLOW_PAYLOAD_NOT_VALID = "workflow_payload_not_valid";

    [Meta<ErrorCode>(WORKFLOW_STATE_NOT_VALID)]
    public const string WORKFLOW_STATE_NOT_VALID = "workflow_state_not_valid";

    [Meta<ErrorCode>(WORKFLOW_SESSION_NOT_VALID)]
    public const string WORKFLOW_SESSION_NOT_VALID = "workflow_session_not_valid";

    [Meta<ErrorCode>(WORKFLOW_CALL_FUNC_NOT_VALID)]
    public const string WORKFLOW_CALL_FUNC_NOT_VALID = "workflow_call_func_not_valid";

    [Meta<ErrorCode>(WORKFLOW_NODE_VALUE_TYPE_NOT_VALID)]
    public const string WORKFLOW_NODE_VALUE_TYPE_NOT_VALID = "workflow_node_type_value_type_not_valid";

    [Meta<ErrorCode>(WORKFLOW_NODE_PAYLOAD_NOT_VALID)]
    public const string WORKFLOW_NODE_PAYLOAD_NOT_VALID = "workflow_node_payload_not_valid";

    [Meta<ErrorCode>(WORKFLOW_EVENT_NOT_VALID)]
    public const string WORKFLOW_EVENT_NOT_VALID = "workflow_event_not_valid";
}
