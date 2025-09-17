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
    public const string NS_SYSTEM_RANGEDATE = "system.rangedate";
    public const string NS_SYSTEM_RANGEFULLDATE = "system.rangefulldate";
    public const string NS_SYSTEM_RANGEMONTH = "system.rangemonth";
    public const string NS_SYSTEM_RANGEYEAR = "system.rangeyear";
    public const string NS_SYSTEM_STRINGS = "system.strings";
    public const string NS_SYSTEM_NUMBERS = "system.numbers";
    public const string NS_SYSTEM_INTS = "system.ints";
    
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
    
    #endregion
}