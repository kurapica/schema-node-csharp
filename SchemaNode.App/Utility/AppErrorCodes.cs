using SchemaNode.Attribute;
using SchemaNode.Property.Record;

namespace SchemaNode.Utility;

internal static class AppErrorCodes
{
    [Meta<ErrorCode>(APP_NOT_FOUND)]
    public const string APP_NOT_FOUND = "app_not_found";
    
    [Meta<ErrorCode>(APP_FIELD_NOT_FOUND)]
    public const string APP_FIELD_NOT_FOUND = "app_field_not_found";
    
    [Meta<ErrorCode>(APP_WORKFLOW_NOT_FOUND)]
    public const string APP_WORKFLOW_NOT_FOUND = "app_workflow_not_found";
    
    [Meta<ErrorCode>(APP_WORKFLOW_NOT_START)]
    public const string APP_WORKFLOW_NOT_START = "app_workflow_not_start";
    
    [Meta<ErrorCode>(APP_WORKFLOW_NODE_NOT_FOUND)]
    public const string APP_WORKFLOW_NODE_NOT_FOUND  = "app_workflow_node_not_found";
    
    [Meta<ErrorCode>(APP_WORKFLOW_NODE_NOT_RUNNING)]
    public const string APP_WORKFLOW_NODE_NOT_RUNNING  = "app_workflow_node_not_running";
    
    [Meta<ErrorCode>(APP_DUMPICATE_FIELD)]
    public const string APP_DUMPICATE_FIELD = "app_duplicate_field";
    
    [Meta<ErrorCode>(APP_FIELD_TYPE_NOT_VALID)]
    public const string APP_FIELD_TYPE_NOT_VALID = "app_field_type_not_valid";
    
    [Meta<ErrorCode>(APP_FIELD_PUSH_FUNC_NOT_VALID)]
    public const string APP_FIELD_PUSH_FUNC_NOT_VALID = "app_field_push_func_not_valid";
    
    [Meta<ErrorCode>(APP_POLICY_EVALUATOR_NOT_VALID)]
    public const string APP_POLICY_EVALUATOR_NOT_VALID = "app_policy_evaluator_not_valid";
    
    [Meta<ErrorCode>(APP_ROW_AUTH_EVALUATOR_NOT_VALID)]
    public const string APP_ROW_AUTH_EVALUATOR_NOT_VALID = "app_row_auth_evaluator_not_valid";
    
    [Meta<ErrorCode>(APP_ROW_AUTH_FILTER_NOT_VALID)]
    public const string APP_ROW_AUTH_FILTER_NOT_VALID = "app_row_auth_filter_not_valid";
    
    [Meta<ErrorCode>(APP_COL_AUTH_FIELD_NOT_FOUND)]
    public const string APP_COL_AUTH_FIELD_NOT_FOUND = "app_col_auth_field_not_found";
    
    [Meta<ErrorCode>(APP_COL_AUTH_EVALUATOR_NOT_VALID)]
    public const string APP_COL_AUTH_EVALUATOR_NOT_VALID = "app_col_auth_validator_not_valid";
    
    [Meta<ErrorCode>(APP_FIELD_FILTER_NOT_VALID)]
    public const string APP_FIELD_FILTER_NOT_VALID = "app_field_filter_not_valid";
    
    [Meta<ErrorCode>(APP_FIELD_FOREIGN_NOT_VALID)]
    public const string APP_FIELD_FOREIGN_NOT_VALID = "app_field_foreign_not_valid";
    
    [Meta<ErrorCode>(APP_FIELD_VIEW_NOT_VALID)]
    public const string APP_FIELD_VIEW_NOT_VALID = "app_field_view_not_valid";

    [Meta<ErrorCode>(APP_DATA_PROVIDER_NOT_EXIST)]
    public const string APP_DATA_PROVIDER_NOT_EXIST = "app_data_provider_not_exist";
    
    [Meta<ErrorCode>(APP_TARGET_POLICY_CANT_CHANGE)]
    public const string APP_TARGET_POLICY_CANT_CHANGE = "app_target_policy_cant_change";

    [Meta<ErrorCode>(APP_ISOLATION_CONTEXT_POLICY_MISSING_MAP)]
    public const string APP_ISOLATION_CONTEXT_POLICY_MISSING_MAP = "app_isolation_context_policy_missing_map";
    
    [Meta<ErrorCode>(FUNC_IS_NOT_POLICY_FILTER)]
    public const string FUNC_IS_NOT_POLICY_FILTER = "func_is_not_policy_filter";
    
    [Meta<ErrorCode>(FUNC_IS_NOT_PUSH_FUNC)]
    public const string FUNC_IS_NOT_PUSH_FUNC = "func_is_not_push_func";
    
    [Meta<ErrorCode>(EVENT_PAYLOAD_NOT_VALID)]
    public const string EVENT_PAYLOAD_NOT_VALID = "event_payload_not_valid";
    
    [Meta<ErrorCode>(EVENT_PAYLOAD_EVALUATOR_NOT_VALID)]
    public const string EVENT_PAYLOAD_EVALUATOR_NOT_VALID = "event_payload_evaluator_not_valid";
    
    [Meta<ErrorCode>(WORKFLOW_PAYLOAD_NOT_VALID)]
    public const string WORKFLOW_PAYLOAD_NOT_VALID = "workflow_payment_not_valid";
    
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
    
    [Meta<ErrorCode>(APP_PUSH_DATA_REQUIRED)]
    public const string APP_PUSH_DATA_REQUIRED = "app_push_data_required";
    
    [Meta<ErrorCode>(APP_TARGET_REQUIRED)]
    public const string APP_TARGET_REQUIRED = "app_target_required";
}
