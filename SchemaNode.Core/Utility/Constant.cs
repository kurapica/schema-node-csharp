// ReSharper disable InconsistentNaming

using SchemaNode.Function;

namespace SchemaNode.Utility;

/// <summary>
/// The constant
/// </summary>
public static class Constant
{
    #region Constraint

    public const int LANGUAGE_MAX_LEN = 8;
    public const int PRIMARY_KEY_MAX_LEN = 128;
    public const int ENTITY_PRIMARY_KEY_MAX_LEN = 128;

    #endregion

    #region Pattern

    public const string REGEX_GENERIC_TYPE = @"^T\d*$";
    public const string REGEX_GENERIC_IMPLEMENT = @"^(\w+)<(.+)>$";

    #endregion

    #region Relation

    public const string ARRAY_ITSELF = "$array";
    public const string ARRAY_ELEMENT = "$element";
    public const string NODE_SELF = "$self";

    #endregion

    #region Schema Kind

    public const string SCHEMA_KIND_NODE = "node";
    public const string SCHEMA_KIND_NAMESPACE = "namespace";
    public const string SCHEMA_KIND_OBJECT = "object";
    public const string SCHEMA_KIND_BOOL = "bool";
    public const string SCHEMA_KIND_INT = "int";
    public const string SCHEMA_KIND_DECIMAL = "decimal";
    public const string SCHEMA_KIND_STRING = "string";
    public const string SCHEMA_KIND_DATE = "date";
    public const string SCHEMA_KIND_ENUM = "enum";
    public const string SCHEMA_KIND_STRUCT = "struct";
    public const string SCHEMA_KIND_ARRAY = "array";
    public const string SCHEMA_KIND_FUNCTION = "function";
    public const string SCHEMA_KIND_PROPERTY = "property";
    public const string SCHEMA_KIND_STRUCT_FIELD = "structfield";
    public const string SCHEMA_KIND_ENUM_VALUE = "enumvalue";
    public const string SCHEMA_KIND_RELATION = "relation";

    public const int SCHEMA_KIND_ORDER_NODE = 0;
    public const int SCHEMA_KIND_ORDER_NAMESPACE = 1;
    public const int SCHEMA_KIND_ORDER_OBJECT = 2;
    public const int SCHEMA_KIND_ORDER_BOOL = 3;
    public const int SCHEMA_KIND_ORDER_INT = 4;
    public const int SCHEMA_KIND_ORDER_DECIMAL = 5;
    public const int SCHEMA_KIND_ORDER_STRING = 6;
    public const int SCHEMA_KIND_ORDER_DATE = 7;
    public const int SCHEMA_KIND_ORDER_ENUM = 8;
    public const int SCHEMA_KIND_ORDER_STRUCT = 9;
    public const int SCHEMA_KIND_ORDER_ARRAY = 10;
    public const int SCHEMA_KIND_ORDER_FUNC = 11;
    public const int SCHEMA_KIND_ORDER_PROP = 12;
    public const int SCHEMA_KIND_ORDER_RELATION = 13;
    public const int SCHEMA_KIND_ORDER_ENUM_VALUE = 14;
    public const int SCHEMA_KIND_ORDER_STRUCT_FIELD = 15;
    
    #endregion

    #region System

    public const string NS_GENERIC_TYPE = "T";
    
    public const string NS_SYSTEM = "system";

    #region Data Types

    // scalar
    public const string NS_SYSTEM_OBJECT = "system.object"; // any value
    public const string NS_SYSTEM_ARRAY = "system.array"; // any array
    public const string NS_SYSTEM_LIST = "system.list"; // generic array type
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
    public const string NS_SYSTEM_CHAR = "system.char";
    public const string NS_SYSTEM_YEAR = "system.year";
    public const string NS_SYSTEM_YEARMONTH = "system.yearmonth";
    public const string NS_SYSTEM_GUID = "system.guid";
    public const string NS_SYSTEM_RANGE_DATE = "system.rangedate";
    public const string NS_SYSTEM_RANGE_FULL_DATE = "system.rangefulldate";
    public const string NS_SYSTEM_RANGE_MONTH = "system.rangemonth";
    public const string NS_SYSTEM_RANGE_YEAR = "system.rangeyear";
    public const string NS_SYSTEM_IDENTIFIER = "system.identifier";
    public const string NS_SYSTEM_PROPERTY = "system.property";

    // language x translate x entry
    public const string NS_SYSTEM_LANGUAGE = "system.language";
    public const string NS_SYSTEM_LOCALE_STRING = "system.localestring";
    public const string NS_SYSTEM_LOCALE_TRAN = "system.localetran";

    // entry for white list
    public const string NS_SYSTEM_ENTRY = "system.entry";

    #endregion

    #region Schema

    // system.schema
    public const string NS_SYSTEM_SCHEMA = "system.schema";
    public const string NS_SYSTEM_SCHEMA_KIND = "system.schema.kind";
    public const string NS_SYSTEM_SCHEMA_NODE = "system.schema.node";
    public const string NS_SYSTEM_SCHEMA_NODE_VALUE_KIND = "system.schema.node.valuekind";
    public const string NS_SYSTEM_SCHEMA_NS = "system.schema.namespace";
    public const string NS_SYSTEM_SCHEMA_OBJECT = "system.schema.object";
    public const string NS_SYSTEM_SCHEMA_BOOL = "system.schema.bool";
    public const string NS_SYSTEM_SCHEMA_INT = "system.schema.int";
    public const string NS_SYSTEM_SCHEMA_DECIMAL = "system.schema.decimal";
    public const string NS_SYSTEM_SCHEMA_STRING = "system.schema.string";
    public const string NS_SYSTEM_SCHEMA_DATE = "system.schema.date";
    public const string NS_SYSTEM_SCHEMA_ENUM = "system.schema.enum";
    public const string NS_SYSTEM_SCHEMA_STRUCT = "system.schema.struct";
    public const string NS_SYSTEM_SCHEMA_STRUCT_FIELD = "system.schema.struct.field";
    public const string NS_SYSTEM_SCHEMA_ARRAY = "system.schema.array";
    public const string NS_SYSTEM_SCHEMA_FUNC = "system.schema.func";
    public const string NS_SYSTEM_SCHEMA_RELATION = "system.schema.relation";
    public const string NS_SYSTEM_SCHEMA_PROPERTY = "system.schema.property";
    
    public const string NS_SYSTEM_SCHEMA_ERROR = "system.schema.error";
    
    #endregion

    #region Function

    public const string NS_SYSTEM_INTRINSIC = "system.intrinsic";
    public const string NS_SYSTEM_MATH = "system.math";
    public const string NS_SYSTEM_LOGIC = "system.logic";
    public const string NS_SYSTEM_CALENDAR = "system.calendar";
    public const string NS_SYSTEM_COLLECTION = "system.collection";
    public const string NS_SYSTEM_DATA = "system.data";
    public const string NS_SYSTEM_STR = "system.str";

    public const string NS_SYSTEM_LOGIC_EQ = $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.eq)}";
    
    #endregion

    #endregion

    #region Function 
    
    #region Expression Priority

    public const int EXP_INTRINSIC_PRIORITY = 100;
    public const int EXP_LOGIC_PRIORITY = 90;
    public const int EXP_ARITHMETIC_PRIORITY = 80;
    public const int EXP_COLLECTION_PRIORITY = 70;
    public const int EXP_DATA_SOURCE_PRIORITY = 60;

    #endregion

    #endregion

    #region Deprecated Schema

    /*
    public const string NS_GENERIC_TYPE = "T";
    public const string NS_GENERIC_TYPE_1 = "T1";
    public const string NS_GENERIC_TYPE_2 = "T2";
    public const string NS_GENERIC_TYPE_3 = "T3";
    public const string NS_GENERIC_TYPE_4 = "T4";
    public const string REGEX_GENERIC_TYPE = @"^T\d*$";
    public const string REGEX_GENERIC_IMPLEMENT = @"^(\w+)<(.+)>$";

    // Represents the special node used as function arguments for validation or relation

    public const string NS_SYSTEM_SCHEMA_PROPERTY = $"{NS_SYSTEM_SCHEMA}.property";

    // system.type
    public const string NS_SYSTEM_SCHEMA_KIND = $"{NS_SYSTEM_SCHEMA}.kind";
    public const string NS_SYSTEM_SCHEMA_KIND_ANY = $"{NS_SYSTEM_SCHEMA_KIND}.any";
    public const string NS_SYSTEM_SCHEMA_KIND_NAMESPACE = $"{NS_SYSTEM_SCHEMA_KIND}.namespace";
    public const string NS_SYSTEM_SCHEMA_KIND_SCALAR = $"{NS_SYSTEM_SCHEMA_KIND}.scalar";
    public const string NS_SYSTEM_SCHEMA_KIND_ENUM = $"{NS_SYSTEM_SCHEMA_KIND}.enum";
    public const string NS_SYSTEM_SCHEMA_KIND_STRUCT = $"{NS_SYSTEM_SCHEMA_KIND}.struct";
    public const string NS_SYSTEM_SCHEMA_KIND_ARRAY = $"{NS_SYSTEM_SCHEMA_KIND}.array";
    public const string NS_SYSTEM_SCHEMA_KIND_FUNC = $"{NS_SYSTEM_SCHEMA_KIND}.func";
    public const string NS_SYSTEM_SCHEMA_KIND_EVENT = $"{NS_SYSTEM_SCHEMA_KIND}.event";
    public const string NS_SYSTEM_SCHEMA_KIND_WORKFLOW = $"{NS_SYSTEM_SCHEMA_KIND}.workflow";
    public const string NS_SYSTEM_SCHEMA_KIND_POLICY = $"{NS_SYSTEM_SCHEMA_KIND}.policy";

    public const string NS_SYSTEM_SCHEMA_KIND_RECOGNIZER = $"{NS_SYSTEM_SCHEMA_KIND}.recognizer";
    public const string NS_SYSTEM_SCHEMA_KIND_PROPERTY = $"{NS_SYSTEM_SCHEMA_KIND}.property";

    public const string NS_SYSTEM_SCHEMA_KIND_RULE = $"{NS_SYSTEM_SCHEMA_KIND}.rule";
    public const string NS_SYSTEM_SCHEMA_KIND_RULE_ARELE = $"{NS_SYSTEM_SCHEMA_KIND_RULE}.arrayelement";
    public const string NS_SYSTEM_SCHEMA_KIND_RULE_VALUE = $"{NS_SYSTEM_SCHEMA_KIND_RULE}.value";
    public const string NS_SYSTEM_SCHEMA_KIND_RULE_VALID = $"{NS_SYSTEM_SCHEMA_KIND_RULE}.valid";
    public const string NS_SYSTEM_SCHEMA_KIND_RULE_UNIONVALID = $"{NS_SYSTEM_SCHEMA_KIND_RULE}.unionvalid";
    public const string NS_SYSTEM_SCHEMA_KIND_RULE_WHITELIST = $"{NS_SYSTEM_SCHEMA_KIND_RULE}.whitelist";
    public const string NS_SYSTEM_SCHEMA_KIND_RULE_EVALUATOR = $"{NS_SYSTEM_SCHEMA_KIND_RULE}.evaluator";
    public const string NS_SYSTEM_SCHEMA_KIND_RULE_PREDICATE = $"{NS_SYSTEM_SCHEMA_KIND_RULE}.predicate";
    
    public const string NS_SYSTEM_SCHEMA_DOMAIN = $"{NS_SYSTEM_SCHEMA}.domain";
    public const string NS_SYSTEM_SCHEMA_DOMAIN_APP = $"{NS_SYSTEM_SCHEMA_DOMAIN}.app";
    public const string NS_SYSTEM_SCHEMA_DOMAIN_FIELD = $"{NS_SYSTEM_SCHEMA_DOMAIN}.field";
    public const string NS_SYSTEM_SCHEMA_DOMAIN_WORKFLOW = $"{NS_SYSTEM_SCHEMA_DOMAIN}.workflow";
    public const string NS_SYSTEM_SCHEMA_DOMAIN_TARGET = $"{NS_SYSTEM_SCHEMA_DOMAIN}.target";

    public const string NS_SYSTEM_SCHEMA_DEF = $"{NS_SYSTEM_SCHEMA}.def";
    public const string NS_SYSTEM_SCHEMA_DEF_NS = $"{NS_SYSTEM_SCHEMA_DEF}.namespace";
    public const string NS_SYSTEM_SCHEMA_DEF_SCALAR = $"{NS_SYSTEM_SCHEMA_DEF}.scalar";
    public const string NS_SYSTEM_SCHEMA_DEF_ENUM = $"{NS_SYSTEM_SCHEMA_DEF}.enum";
    public const string NS_SYSTEM_SCHEMA_DEF_STRUCT = $"{NS_SYSTEM_SCHEMA_DEF}.struct";
    public const string NS_SYSTEM_SCHEMA_DEF_ARRAY = $"{NS_SYSTEM_SCHEMA_DEF}.array";
    public const string NS_SYSTEM_SCHEMA_DEF_FUNC = $"{NS_SYSTEM_SCHEMA_DEF}.func";
    public const string NS_SYSTEM_SCHEMA_DEF_POLICY = $"{NS_SYSTEM_SCHEMA_DEF}.policy";
    public const string NS_SYSTEM_SCHEMA_DEF_EVENT = $"{NS_SYSTEM_SCHEMA_DEF}.event";
    public const string NS_SYSTEM_SCHEMA_DEF_WORKFLOW = $"{NS_SYSTEM_SCHEMA_DEF}.workflow";
    public const string NS_SYSTEM_SCHEMA_DEF_RECOGNIZER = $"{NS_SYSTEM_SCHEMA_DEF}.recognizer";
    public const string NS_SYSTEM_SCHEMA_DEF_PROPERTY = $"{NS_SYSTEM_SCHEMA_DEF}.property";
    public const string NS_SYSTEM_SCHEMA_DEF_APP = $"{NS_SYSTEM_SCHEMA_DEF}.app";
    public const string NS_SYSTEM_SCHEMA_DEF_APP_FIELD = $"{NS_SYSTEM_SCHEMA_DEF_APP}.field";
    public const string NS_SYSTEM_SCHEMA_DEF_APP_WORKFLOW = $"{NS_SYSTEM_SCHEMA_DEF_APP}.workflow";

    // function namespace
    public const string NS_SYSTEM_INTRINSIC = "system.intrinsic";
    public const string NS_SYSTEM_MATH = "system.math";
    public const string NS_SYSTEM_LOGIC = "system.logic";
    public const string NS_SYSTEM_CALENDAR = "system.calendar";
    public const string NS_SYSTEM_COLLECTION = "system.collection";
    public const string NS_SYSTEM_DATA = "system.data";
    
    // workflow namespace
    public const string NS_SYSTEM_WORKFLOW = "system.workflow";
    public const string NS_SYSTEM_WORKFLOW_ID = "system.workflow.id";
    public const string NS_SYSTEM_WORKFLOW_CRON = "system.workflow.cron";
    public const string NS_SYSTEM_WORKFLOW_NODE = "system.workflow.node";
    public const string NS_SYSTEM_WORKFLOW_CONTROL = "system.workflow.control";
    public const string NS_SYSTEM_WORKFLOW_EVENT = "system.workflow.event";
    public const string NS_SYSTEM_WORKFLOW_FUNC = "system.workflow.func";
    public const string NS_SYSTEM_WORKFLOW_INTERACTION = "system.workflow.interaction";
    
    // property namespace
    public const string NS_SYSTEM_PROPERTY = "system.property";

    // event namespace
    public const string NS_SYSTEM_EVENT = "system.event";
    
    // context struct
    public const string NS_SYSTEM_CONTEXT = "system.context";

    // core property
    public const string PROPERTY_UPLIMIT = "upLimit";
    public const string PROPERTY_LOWLIMIT = "lowLimit";
    public const string PROPERTY_DEFAULT = "default";
    public const string PROPERTY_TYPE = "type";

    // static function sign
    public const int FUNC_SIGN_CONTEXT = 1;
    public const int FUNC_SIGN_ASYNC = 2;
    public const int FUNC_SIGN_GENERIC = 4;
    public const int FUNC_SIGN_IMMUTABLE = 8;
    public const int FUNC_SIGN_REMOTE_CALL = 16;
    public const int FUNC_SIGN_NULLABLE_RET = 32;

    // Topic
    public const char TOPIC_SEP = '/';
    public const string TOPIC_WILDCARD_SINGLE = "+";
    public const string TOPIC_WILDCARD_MULTI = "*";
    public const string TOPIC_WILDCARD_ALL = "#";

    /// <summary>
    /// DYNAMIC TABLE TARG FIELD
    /// </summary>
    public const string DYNAMIC_TABLE_TARG_FIELD = "_target";

    /// <summary>
    /// DYNAMIC TABLE TARG LEN
    /// </summary>
    public const int DYNAMIC_TABLE_TARG_LEN = 64;

    /// <summary>
    /// The max length for EAV table field name, to avoid abuse of long field name which may cause performance issue. 
    /// </summary>
    public const int EAV_TABLE_FIELD_MAX_LENGTH = 64;

    /// <summary>
    /// The max combine case count, to avoid abuse of too many cases which may cause performance issue.
    /// </summary>
    public const int MAX_COMBINE_CASE_COUNT = 15;
    
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
    /// Entity-ATTRIBUTE-VALUE TABLE PREFIX
    /// </summary>
    public const string EAV_TABLE_PREFIX = "eav";

    /// <summary>
    /// The field name for EAV table to store the attribute name
    /// </summary>
    public const string EAV_TABLE_FIELD = "_field";
    
    /// <summary>
    /// The big int field
    /// </summary>
    public const string EAV_TABLE_BIGINT_FIELD = "_bigint";

    /// <summary>
    /// The double field
    /// </summary>
    public const string EAV_TABLE_DOUBLE_FIELD = "_double";
    
    /// <summary>
    /// The index-able string with 128 max length
    /// </summary>
    public const string EAV_TABLE_STRING_FIELD = "_str";
    
    /// <summary>
    /// The text field
    /// </summary>
    public const string EAV_TABLE_TEXT_FIELD = "_text";
    
    /// <summary>
    /// The datetime field
    /// </summary>
    public const string EAV_TABLE_DATETIME_FIELD = "_datetime";
    
    /// <summary>
    /// The JSON field
    /// </summary>
    public const string EAV_TABLE_JSON_FIELD = "_json";
    
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

    #region Expression Priority

    public const int EXP_INTRINSIC_PRIORITY = 100;
    public const int EXP_LOGIC_PRIORITY = 90;
    public const int EXP_ARITHMETIC_PRIORITY = 80;
    public const int EXP_COLLECTION_PRIORITY = 70;
    public const int EXP_DATA_SOURCE_PRIORITY = 60;

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
    public const string TYPE_FUNC_COMPILE_ERROR = "TYPE_FUNC_COMPILE_ERROR";
    public const string TYPE_FUNC_NOT_VALID_FOR_POLICY_FILTER = "TYPE_FUNC_NOT_VALID_FOR_POLICY_FILTER";

    public const string TYPE_FUNC_EXP_NAME_REQUIRED = "TYPE_FUNC_EXP_NAME_REQUIRED";
    public const string TYPE_FUNC_EXP_NAME_CONFLICT_ARG = "TYPE_FUNC_EXP_NAME_CONFLICT_ARG";
    public const string TYPE_FUNC_EXP_CALL_FUNC_REQUIRED = "TYPE_FUNC_EXP_CALL_FUNC_REQUIRED";
    public const string TYPE_FUNC_EXP_CALL_FUNC_NOT_EXIST = "TYPE_FUNC_EXP_CALL_FUNC_NOT_EXIST";
    public const string TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID = "TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID";
    public const string TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID = "TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID";
    public const string TYPE_FUNC_EXP_CALL_CONSTANT_NOT_VALID = "TYPE_FUNC_EXP_CALL_CONSTANT_NOT_VALD";
    public const string TYPE_FUNC_EXP_ARGS_NOT_VALID = "TYPE_FUNC_EXP_ARGS_NOT_VALID";
    public const string TYPE_FUNC_EXP_CALL_NO_ARRAY = "TYPE_FUNC_EXP_CALL_NO_ARRAY";
    public const string TYPE_FUNC_CALL_ARG_COUNT_NOT_MATCH = "TYPE_FUNC_CALL_ARG_COUNT_NOT_MATCH";
    public const string TYPE_FUNC_CALL_ARG_NOT_EXIST = "TYPE_FUNC_CALL_ARG_NOT_EXIST";
    public const string TYPE_FUNC_CALL_ARG_TYPE_NOT_MATCH_CALL = "TYPE_FUNC_CALL_ARG_TYPE_NOT_MATCH_CALL";

    public const string TYPE_ENUM_VALUE_HAS_SUBLIST = "TYPE_ENUM_VALUE_HAS_SUBLIST";
    
    public const string APP_NOT_FOUND = "APP_NOT_FOUND";
    public const string APP_FIELD_NOT_FOUND = "APP_FIELD_NOT_FOUND";
    public const string APP_FIELD_TYPE_NOT_VALID = "APP_FIELD_TYPE_NOT_VALID";
    public const string APP_TARGET_REQUIRED = "APP_TARGET_REQUIRED";
    public const string APP_PUSH_DATA_REQUIRED = "APP_PUSH_DATA_REQUIRED";
    public const string APP_DATA_PROVIDER_NOT_EXIST = "APP_DATA_PROVIDER_NOT_EXIST";
    public const string APP_PUSH_DATA_WRONG_FUNC = "APP_PUSH_DATA_WRONG_FUNC";
    public const string APP_TARGET_POLICY_CANT_CHANGE = "APP_TARGET_POLICY_CANT_CHANGE";
    public const string APP_ISOLATION_CONTEXT_POLICY_MISSING_MAP = "APP_ISOLATION_CONTEXT_POLICY_MISSING_MAP";

    public const string WORKFLOW_NOT_FOUND = "WORKFLOW_NOT_FOUND";
    public const string WORKFLOW_NODE_NOT_FOUND = "WORKFLOW_NODE_NOT_FOUND";
    public const string WORKFLOW_NOT_START = "WORKFLOW_NOT_START";
    public const string WORKFLOW_NODE_NOT_RUNNING = "WORKFLOW_NODE_NOT_RUNNING";
    public const string WORKFLOW_NODE_PAYLOAD_TYPE_NOT_VALID = "WORKFLOW_NODE_PAYLOAD_TYPE_NOT_VALID";
    
    */
    #endregion
}