namespace SchemaNode.App;

/// <summary>
/// The App-layer schema node status codes, used to describe the loading result of App/AppField/AppWorkflow/Policy types.
/// </summary>
public static class AppStatus
{
    public const string Ready = "ready";
    public const string PolicyWrongFunc = "policy_wrong_func";
    public const string ApplicationInvalidField = "application_invalid_field";
    public const string ApplicationFieldWrongType = "application_field_wrong_type";
    public const string ApplicationFieldWrongFunc = "application_field_wrong_func";
    public const string ApplicationFieldWrongFuncField = "application_field_wrong_func_field";
    public const string ApplicationFieldWrongRef = "application_field_wrong_ref";
    public const string ApplicationRelationWrongTarget = "application_relation_wrong_target";
    public const string ApplicationRelationWrongFunc = "application_relation_wrong_func";
    public const string ApplicationDataAuthWrongFunc = "application_data_auth_wrong_func";
    public const string ApplicationFieldDataAuthWrongFunc = "application_field_data_auth_wrong_func";
    public const string ApplicationFieldDataAuthWrongField = "application_field_data_auth_wrong_field";
    public const string ApplicationPushDataWrongFunc = "application_push_data_wrong_func";
    public const string ApplicationFieldDataWrongFilter = "application_field_data_wrong_filter";
    public const string WorkflowWrongPayload = "workflow_wrong_payload";
}
