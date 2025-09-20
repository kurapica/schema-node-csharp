// ReSharper disable InconsistentNaming
namespace SchemaNode.Utility;

/// <summary>
/// The constant
/// </summary>
public static class Constant
{
    #region Schema

    public const string NS_SYSTEM = "system";
    public const string NS_SYSTEM_ARRAY = "system.array";
    public const string NS_SYSTEM_STRUCT = "system.struct";
    public const string NS_SYSTEM_BOOL = "system.bool";
    public const string NS_SYSTEM_DATE = "system.date";
    public const string NS_SYSTEM_NUMBER = "system.number";
    public const string NS_SYSTEM_DOUBLE = "system.double";
    public const string NS_SYSTEM_FLOAT = "system.float";
    public const string NS_SYSTEM_PERCENT = "system.percent";
    public const string NS_SYSTEM_FULLDATE = "system.fulldate";
    public const string NS_SYSTEM_INT = "system.int";
    public const string NS_SYSTEM_STRING = "system.string";
    public const string NS_SYSTEM_YEAR = "system.year";
    public const string NS_SYSTEM_YEARMONTH = "system.yearmonth";
    public const string NS_SYSTEM_GUID = "system.guid";
    public const string NS_SYSTEM_RANGEDATE = "system.rangedate";
    public const string NS_SYSTEM_RANGEFULLDATE = "system.rangefulldate";
    public const string NS_SYSTEM_RANGEMONTH = "system.rangemonth";
    public const string NS_SYSTEM_RANGEYEAR = "system.rangeyear";
    public const string NS_SYSTEM_STRINGS = "system.strings";
    public const string NS_SYSTEM_NUMBERS = "system.numbers";
    public const string NS_SYSTEM_INTS = "system.ints";

    public const string NS_SYSTEM_CONV = "system.conv";
    public const string NS_SYSTEM_MATH = "system.math";
    public const string NS_SYSTEM_LOGIC = "system.logic";

    public const int FUNC_SIGN_CONTEXT = 1;
    public const int FUNC_SIGN_ASYNC = 2;
    public const int FUNC_SIGN_GENERIC = 4;
    public const int FUNC_SIGN_IMMUTABLE = 8;
    public const int FUNC_SIGN_SERVERCALL = 16;

    #endregion
    
    #region Message

    public const string TYPE_VALUE_NOT_VALID = "TYPE_VALUE_NOT_VALID";
    public const string TYPE_NAMESPACE_NOT_DATA_TYPE = "TYPE_NAMESPACE_NOT_DATA_TYPE";
    public const string TYPE_VALUE_STRUCT_MEMBER_REQUIRE = "TYPE_VALUE_STRUCT_MEMBER_REQUIRE";

    public const string TYPE_FUNC_NO_DEFINITION = "TYPE_FUNC_NO_DEFINITION";
    public const string TYPE_FUNC_ARG_NAME_REQUIRED = "TYPE_FUNC_ARG_NAME_REQUIRED";
    public const string TYPE_FUNC_ARG_NAME_DUPLICATE = "TYPE_FUNC_ARG_NAME_DUPLICATE";
    public const string TYPE_FUNC_ARG_TYPE_NOT_VALID = "TYPE_FUNC_ARG_TYPE_NOT_VALID";
    public const string TYPE_FUNC_ARG_NO_TYPE = "TYPE_FUNC_ARG_NO_TYPE";
    public const string TYPE_FUNC_NEED_EXPS = "TYPE_FUNC_NEED_EXPS";
    public const string TYPE_FUNC_RETURN_NOT_VALID = "TYPE_FUNC_RETURN_NOT_VALID";
    public const string TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID = "TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID";

    public const string TYPE_FUNC_EXP_NAME_REQUIRED = "TYPE_FUNC_EXP_NAME_REQUIRED";
    public const string TYPE_FUNC_EXP_NAME_CONFLICT_ARG = "TYPE_FUNC_EXP_NAME_CONFLICT_ARG";
    public const string TYPE_FUNC_EXP_CALL_FUNC_REQUIRED = "TYPE_FUNC_EXP_CALL_FUNC_REQUIRED";
    public const string TYPE_FUNC_EXP_CALL_FUNC_NOT_EXIST = "TYPE_FUNC_EXP_CALL_FUNC_NOT_EXIST";
    public const string TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID = "TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID";
    public const string TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID = "TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID";
    public const string TYPE_FUNC_EXP_CALL_CONSTANT_NOT_VALID = "TYPE_FUNC_EXP_CALL_CONSTANT_NOT_VALD";
    public const string TYPE_FUNC_EXP_ARGS_NOT_VALID = "TYPE_FUNC_EXP_ARGS_NOT_VALID";
    public const string TYPE_FUNC_EXP_CALL_NO_ARRAY = "TYPE_FUNC_EXP_CALL_NO_ARRAY";
    public const string TYPE_FUNC_CANT_USE_AS_REDUCE = "TYPE_FUNC_CANT_USE_AS_REDUCE";
    public const string TYPE_FUNC_CANT_USE_AS_FIRST = "TYPE_FUNC_CANT_USE_AS_FIRST";
    public const string TYPE_FUNC_CANT_USE_AS_LAST = "TYPE_FUNC_CANT_USE_AS_LAST";
    public const string TYPE_FUNC_CANT_USE_AS_FILTER = "TYPE_FUNC_CANT_USE_AS_FILTER";
    public const string TYPE_FUNC_CALL_ARG_COUNT_NOT_MATCH = "TYPE_FUNC_CALL_ARG_COUNT_NOT_MATCH";
    public const string TYPE_FUNC_CALL_ARG_NOT_EXIST = "TYPE_FUNC_CALL_ARG_NOT_EXIST";
    public const string TYPE_FUNC_CALL_ARG_TYPE_NOT_MATCH_CALL = "TYPE_FUNC_CALL_ARG_TYPE_NOT_MATCH_CALL";

    #endregion
}