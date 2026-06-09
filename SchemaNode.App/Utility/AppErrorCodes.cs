using SchemaNode.Attribute;
using SchemaNode.Property.Record;

namespace SchemaNode.Utility;

internal static class AppErrorCodes
{
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

    public const string APP_DATA_PROVIDER_NOT_EXIST = "AppErrorCodes.APP_DATA_PROVIDER_NOT_EXIST";
}
