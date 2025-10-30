// ReSharper disable InconsistentNaming
namespace SchemaNode.Utility;

/// <summary>
/// The constant
/// </summary>
public static class Constant
{
    #region Schema

    public const int ENTITY_PRIMARY_KEY_MAX_LEN = 128;
    
    public const string NS_SYSTEM = "system";
    public const string NS_SYSTEM_ARRAY = "system.array"; // any array
    public const string NS_SYSTEM_STRUCT = "system.struct"; // any struct
    public const string NS_SYSTEM_JSON = "system.json"; // any value, used by entity
    public const string NS_SYSTEM_BOOL = "system.bool";
    public const string NS_SYSTEM_DATE = "system.date";
    public const string NS_SYSTEM_NUMBER = "system.number";
    public const string NS_SYSTEM_DOUBLE = "system.double";
    public const string NS_SYSTEM_FLOAT = "system.float";
    public const string NS_SYSTEM_PERCENT = "system.percent";
    public const string NS_SYSTEM_FULL_DATE = "system.fulldate";
    public const string NS_SYSTEM_INT = "system.int";
    public const string NS_SYSTEM_STRING = "system.string";
    public const string NS_SYSTEM_YEAR = "system.year";
    public const string NS_SYSTEM_YEARMONTH = "system.yearmonth";
    public const string NS_SYSTEM_GUID = "system.guid";
    public const string NS_SYSTEM_RANGE_DATE = "system.rangedate";
    public const string NS_SYSTEM_RANGE_FULL_DATE = "system.rangefulldate";
    public const string NS_SYSTEM_RANGE_MONTH = "system.rangemonth";
    public const string NS_SYSTEM_RANGE_YEAR = "system.rangeyear";
    
    // array
    public const string NS_SYSTEM_STRINGS = "system.strings";
    public const string NS_SYSTEM_NUMBERS = "system.numbers";
    public const string NS_SYSTEM_INTS = "system.ints";

    // language x translate x entry
    public const string NS_SYSTEM_LANGUAGE = "system.language";
    public const string NS_SYSTEM_LOCALE_STRING = "system.localestring";
    public const string NS_SYSTEM_LOCALE_TRAN = "system.localetran";

    public const string NS_SYSTEM_ENTRY = "system.entry";
    public const string NS_SYSTEM_ENTRIES = "system.entrys";
    
    // system.schema
    public const string NS_SYSTEM_SCHEMA = "system.schema";
    public const string NS_SYSTEM_SCHEMA_ANY_TYPE = "system.schema.anytype";
    public const string NS_SYSTEM_SCHEMA_NAMESPACE = "system.schema.namespace";
    public const string NS_SYSTEM_SCHEMA_SCALAR_TYPE = "system.schema.scalartype";
    public const string NS_SYSTEM_SCHEMA_ENUM_TYPE = "system.schema.enumtype";
    public const string NS_SYSTEM_SCHEMA_STRUCT_TYPE = "system.schema.structtype";
    public const string NS_SYSTEM_SCHEMA_ARRAY_TYPE = "system.schema.arraytype";
    public const string NS_SYSTEM_SCHEMA_FUNC_TYPE = "system.schema.functype";
    public const string NS_SYSTEM_SCHEMA_VALID_FUNC_TYPE = "system.schema.validfunc";
    public const string NS_SYSTEM_SCHEMA_WHITELIST_FUNC_TYPE = "system.schema.whitelistfunc";
    public const string NS_SYSTEM_SCHEMA_ARRAY_ELE_TYPE = "system.schema.arrayeletype";
    public const string NS_SYSTEM_SCHEMA_VALUE_TYPE = "system.schema.valuetype";
    public const string NS_SYSTEM_SCHEMA_VAR_NAME = "system.schema.varname";
    public const string NS_SYSTEM_SCHEMA_ANY_VALUE = "system.schema.anyvalue";
    
    public const string NS_SYSTEM_SCHEMA_APP = "system.schema.app";
    public const string NS_SYSTEM_SCHEMA_APP_FIELD = "system.schema.appfield";

    // function namespace
    public const string NS_SYSTEM_CONV = "system.conv";
    public const string NS_SYSTEM_MATH = "system.math";
    public const string NS_SYSTEM_LOGIC = "system.logic";
    
    public const string NS_SYSTEM_COLLECTION = "system.collection";

    // static function sign
    public const int FUNC_SIGN_CONTEXT = 1;
    public const int FUNC_SIGN_ASYNC = 2;
    public const int FUNC_SIGN_GENERIC = 4;
    public const int FUNC_SIGN_IMMUTABLE = 8;
    public const int FUNC_SIGN_REMOTE_CALL = 16;
    public const int FUNC_SIGN_NULLABLE_RET = 32;

    /// <summary>
    /// DYNAMIC TABLE TARG FIELD
    /// </summary>
    public const string DYNAMIC_TABLE_TARG_FIELD = "_target";

    /// <summary>
    /// DYNAMIC TABLE TARG LEN
    /// </summary>
    public const int DYNAMIC_TABLE_TARG_LEN = 64;

    /// <summary>
    /// DYNAMIC TABLE VALUE FIELD
    /// </summary>
    public const string DYNAMIC_TABLE_VALUE_FIELD = "_data";

    /// <summary>
    /// DYNAMIC TABLE SEQNO FIELD
    /// </summary>
    public const string DYNAMIC_TABLE_SEQNO_FIELD = "_seqno";

    /// <summary>
    /// DYNAMIC TABLE PREFIX
    /// </summary>
    public const string DYNAMIC_TABLE_PREFIX = "dyn";

    /// <summary>
    /// DYNAMIC UNIQUE INDEX
    /// </summary>
    public const string DYNAMIC_UNIQUE_INDEX = "IDX_Key";

    /// <summary>
    /// COMPLEX SEP
    /// </summary>
    public const string COMPLEX_SEP = "_";

    /// <summary>
    /// The app field ref type
    /// </summary>
    public const string APP_FIELD_REF = "__app_field_ref";

    /// <summary>
    /// The app field refs type
    /// </summary>
    public const string APP_FIELD_REFS = "__app_field_refs";

    /// <summary>
    /// The ref app
    /// </summary>
    public const string APP_FIELD_REF_APP = "app";

    /// <summary>
    /// The ref target
    /// </summary>
    public const string APP_FIELD_REF_TARGET = "target";

    /// <summary>
    /// The field name
    /// </summary>
    public const string APP_FIELD_REF_NAME = "appref";

    #endregion

    #region Message

    public const string TYPE_NOT_EXIST = "TYPE_NOT_EXIST";
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
    
    public const string TYPE_ENUM_VALUE_HAS_SUBLIST = "TYPE_ENUM_VALUE_HAS_SUBLIST";
    
    public const string APP_NOT_FOUND = "APP_NOT_FOUND";
    public const string APP_FIELD_NOT_FOUND = "APP_FIELD_NOT_FOUND";
    public const string APP_TARGET_REQUIRED = "APP_TARGET_REQUIRED";
    public const string APP_PUSH_DATA_REQUIRED = "APP_PUSH_DATA_REQUIRED";
    public const string APP_DATA_PROVIDER_NOT_EXIST = "APP_DATA_PROVIDER_NOT_EXIST";

    #endregion
}