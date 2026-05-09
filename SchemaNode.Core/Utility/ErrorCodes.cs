using SchemaNode.Attribute;
using SchemaNode.Property.Record;

namespace SchemaNode.Utility;

/// <summary>
/// The error codes
/// </summary>
public static class ErrorCodes
{
    [Meta<ErrorCode>(NO_DEFINITION)]
    public const string NO_DEFINITION = "no_definition";
    
    [Meta<ErrorCode>(RELATION_FUNC_NOT_EXIST)]
    public const string RELATION_FUNC_NOT_EXIST = "relation_func_not_exist";

    [Meta<ErrorCode>(STRUCT_VALID_FUNC_NOT_EXIST)]
    public const string STRUCT_VALID_FUNC_NOT_EXIST = "struct_valid_func_not_exist";
    
    [Meta<ErrorCode>(SCALAR_WRONG_BASE)]
    public const string SCALAR_WRONG_BASE = "scalar_wront_base";
    
    [Meta<ErrorCode>(WRONG_REF_TYPE)]
    public const string WRONG_REF_TYPE = "wrong_ref_type";

    [Meta<ErrorCode>(ARRAY_WRONG_ELEMENT)]
    public const string ARRAY_WRONG_ELEMENT = "array_wrong_element";

    [Meta<ErrorCode>(PROP_WRONG_VALUE_TYPE)]
    public const string PROP_WRONG_VALUE_TYPE = "prop_wrong_value_type";

    [Meta<ErrorCode>(STRUCT_WRONG_VALID)]
    public const string STRUCT_WRONG_VALID = "struct_wrong_valid";
    
    [Meta<ErrorCode>(STRUCT_RELATION_WRONG_FUNC)]
    public const string STRUCT_FIELD_WRONG_TYPE = "struct_field_wrong_type";

    [Meta<ErrorCode>(STRUCT_RELATION_WRONG_FIELD)]
    public const string STRUCT_RELATION_WRONG_FIELD = "struct_relation_wrong_field";

    [Meta<ErrorCode>(STRUCT_RELATION_WRONG_PROP)]
    public const string STRUCT_RELATION_WRONG_PROP = "struct_relation_wrong_prop";

    [Meta<ErrorCode>(STRUCT_RELATION_WRONG_FUNC)]
    public const string STRUCT_RELATION_WRONG_FUNC = "struct_relation_wrong_func";

    [Meta<ErrorCode>(STRUCT_RELATION_WRONG_ARGS)]
    public const string STRUCT_RELATION_WRONG_ARGS = "struct_relation_wrong_args";

    [Meta<ErrorCode>(FUNC_NO_EXPS)]
    public const string FUNC_NO_EXPS = "func_no_exps";

    [Meta<ErrorCode>(FUNC_WRONG_RETURN)]
    public const string FUNC_WRONG_RETURN = "func_wrong_return";

    [Meta<ErrorCode>(FUNC_ARG_NO_NAME)]
    public const string FUNC_ARG_NO_NAME = "func_arg_no_name";

    [Meta<ErrorCode>(FUNC_ARG_DUPLICATE_NAME)]
    public const string FUNC_ARG_DUPLICATE_NAME = "func_arg_duplicate_name";

    [Meta<ErrorCode>(FUNC_ARG_NO_TYPE)]
    public const string FUNC_ARG_NO_TYPE = "func_arg_no_type";

    [Meta<ErrorCode>(FUNC_ARG_WRONG_TYPE)]
    public const string FUNC_ARG_WRONG_TYPE = "func_arg_wrong_type";

    [Meta<ErrorCode>(FUNC_EXP_NO_NAME)]
    public const string FUNC_EXP_NO_NAME = "func_exp_no_name";

    [Meta<ErrorCode>(FUNC_EXP_DUPLICATE_NAME)]
    public const string FUNC_EXP_DUPLICATE_NAME = "func_exp_duplicate_name";

    [Meta<ErrorCode>(FUNC_EXP_WRONG_FUNC)]
    public const string FUNC_EXP_WRONG_FUNC = "func_exp_wrong_func";

    [Meta<ErrorCode>(FUNC_EXP_WRONG_ARGS)]
    public const string FUNC_EXP_WRONG_ARGS = "func_exp_wrong_args";

    [Meta<ErrorCode>(FUNC_EXP_WRONG_RETURN)]
    public const string FUNC_EXP_WRONG_RETURN = "func_exp_wrong_return";

    [Meta<ErrorCode>(FUNC_EXP_WRONG_COLLECTION)]
    public const string FUNC_EXP_WRONG_COLLECTION = "func_exp_wrong_collection";

    [Meta<ErrorCode>(FUNC_COMPILE_ERROR)]
    public const string FUNC_COMPILE_ERROR = "func_compile_error";

    [Meta<ErrorCode>(FUNC_RETURN_MEMBER_INVALID)]
    public const string FUNC_RETURN_MEMBER_INVALID = "func_return_member_invalid";

    [Meta<ErrorCode>(FUNC_CANT_BE_POLICY_FILTER)]
    public const string FUNC_CANT_BE_POLICY_FILTER = "func_cant_be_policy_filter";

    [Meta<ErrorCode>(APP_PUSH_DATA_WRONG_FUNC)]
    public const string APP_PUSH_DATA_WRONG_FUNC = "app_push_data_wrong_func";
}