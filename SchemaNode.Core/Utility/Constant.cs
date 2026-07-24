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

    public const string RELATION_OWNER = "$owner";
    public const string ARRAY_PREVIOUS = "$previous";
    public const string ARRAY_ELEMENT = "$element";
    public const string NODE_SELF = "$self";
    public const string NODE_TYPE = "$type";
    public const string ENTRY_ROOT = "$root";

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
    public const string SCHEMA_KIND_RELATION = "relation";
    public const string SCHEMA_KIND_FUNC_ARG = "functionarg";
    public const string SCHEMA_KIND_ENTRY = "entry";

    internal const int SCHEMA_KIND_ORDER_NODE = 0;
    internal const int SCHEMA_KIND_ORDER_NAMESPACE = 1;
    internal const int SCHEMA_KIND_ORDER_OBJECT = 2;
    internal const int SCHEMA_KIND_ORDER_BOOL = 3;
    internal const int SCHEMA_KIND_ORDER_INT = 4;
    internal const int SCHEMA_KIND_ORDER_DECIMAL = 5;
    internal const int SCHEMA_KIND_ORDER_STRING = 6;
    internal const int SCHEMA_KIND_ORDER_DATE = 7;
    internal const int SCHEMA_KIND_ORDER_ENUM = 8;
    internal const int SCHEMA_KIND_ORDER_STRUCT = 9;
    internal const int SCHEMA_KIND_ORDER_ARRAY = 10;
    internal const int SCHEMA_KIND_ORDER_FUNC = 11;
    internal const int SCHEMA_KIND_ORDER_PROP = 12;
    internal const int SCHEMA_KIND_ORDER_RELATION = 13;
    internal const int SCHEMA_KIND_ORDER_STRUCT_FIELD = 15;
    internal const int SCHEMA_KIND_ORDER_FUNC_ARG = 16;
    internal const int SCHEMA_KIND_ORDER_ENTRY = 17;
    
    #endregion

    #region System

    public const string NS_GENERIC_TYPE = "T";
    
    public const string NS_SYSTEM = "system";

    #region Data Types

    // scalar
    public const string NS_SYSTEM_OBJECT = "system.object"; // any value
    public const string NS_SYSTEM_ARRAY = "system.array"; // any array
    public const string NS_SYSTEM_LIST = "system.list"; // generic array type
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
    public const string NS_SYSTEM_CONTEXT  = "system.context";

    // language x translate x entry
    public const string NS_SYSTEM_LANGUAGE = "system.language";
    public const string NS_SYSTEM_LOCALE_STRING = "system.localestring";
    public const string NS_SYSTEM_LOCALE_TRAN = "system.localetran";

    // entry for white list
    public const string NS_SYSTEM_ENTRY = "system.entry";
    public const string NS_SYSTEM_ENTRY_ACCESS = "system.entryaccess";

    #endregion

    #region Schema

    // system.schema
    public const string NS_SYSTEM_SCHEMA = "system.schema";
    public const string NS_SYSTEM_SCHEMA_KIND = $"{NS_SYSTEM_SCHEMA}.kind";
    public const string NS_SYSTEM_SCHEMA_NODE = $"{NS_SYSTEM_SCHEMA}.node";
    public const string NS_SYSTEM_SCHEMA_NODE_VALUE_KIND = $"{NS_SYSTEM_SCHEMA_NODE}.valuekind";
    public const string NS_SYSTEM_SCHEMA_NS = $"{NS_SYSTEM_SCHEMA}.namespace";
    public const string NS_SYSTEM_SCHEMA_OBJECT = $"{NS_SYSTEM_SCHEMA}.object";
    public const string NS_SYSTEM_SCHEMA_BOOL = $"{NS_SYSTEM_SCHEMA}.bool";
    public const string NS_SYSTEM_SCHEMA_INT = $"{NS_SYSTEM_SCHEMA}.int";
    public const string NS_SYSTEM_SCHEMA_DECIMAL = $"{NS_SYSTEM_SCHEMA}.decimal";
    public const string NS_SYSTEM_SCHEMA_STRING = $"{NS_SYSTEM_SCHEMA}.string";
    public const string NS_SYSTEM_SCHEMA_DATE = $"{NS_SYSTEM_SCHEMA}.date";
    public const string NS_SYSTEM_SCHEMA_ENUM = $"{NS_SYSTEM_SCHEMA}.enum";
    public const string NS_SYSTEM_SCHEMA_STRUCT = $"{NS_SYSTEM_SCHEMA}.struct";
    public const string NS_SYSTEM_SCHEMA_STRUCT_FIELD = $"{NS_SYSTEM_SCHEMA_STRUCT}.field";
    public const string NS_SYSTEM_SCHEMA_ARRAY = $"{NS_SYSTEM_SCHEMA}.array";
    public const string NS_SYSTEM_SCHEMA_FUNC = $"{NS_SYSTEM_SCHEMA}.func";
    public const string NS_SYSTEM_SCHEMA_RELATION = $"{NS_SYSTEM_SCHEMA}.relation";
    public const string NS_SYSTEM_SCHEMA_PROPERTY = $"{NS_SYSTEM_SCHEMA}.prop";
    public const string NS_SYSTEM_SCHEMA_PROPERTY_CORE = $"{NS_SYSTEM_SCHEMA_PROPERTY}.core";
    public const string NS_SYSTEM_SCHEMA_PROPERTY_COMMON = $"{NS_SYSTEM_SCHEMA_PROPERTY}.common";
    public const string NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT = $"{NS_SYSTEM_SCHEMA_PROPERTY}.constraint";
    public const string NS_SYSTEM_SCHEMA_PROPERTY_FUNC = $"{NS_SYSTEM_SCHEMA_PROPERTY}.func";
    public const string NS_SYSTEM_SCHEMA_PROPERTY_RELATION = $"{NS_SYSTEM_SCHEMA_PROPERTY}.relation";
    
    public const string NS_SYSTEM_SCHEMA_ERROR = $"{NS_SYSTEM_SCHEMA}.error";

    #endregion

    #region Function

    public const string NS_SYSTEM_INTRINSIC = "system.intrinsic";
    public const string NS_SYSTEM_MATH = "system.math";
    public const string NS_SYSTEM_LOGIC = "system.logic";
    public const string NS_SYSTEM_CALENDAR = "system.calendar";
    public const string NS_SYSTEM_COLLECTION = "system.collection";
    public const string NS_SYSTEM_DATA = "system.data";
    public const string NS_SYSTEM_DATA_ENUM = "system.data.enum";
    public const string NS_SYSTEM_STR = "system.str";

    public const string NS_SYSTEM_SCHEMA_REFLECT = $"{NS_SYSTEM_SCHEMA}.reflect";
    public const string NS_SYSTEM_SCHEMA_REFLECT_FUNC = $"{NS_SYSTEM_SCHEMA}.func";

    public const string NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND = $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isschemakind)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND = $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isvaluekind)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_IS_ARRAY_ELE = $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isarrayele)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES = $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.getsubentries)}";


    public const string NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN = $"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(SystemReflect.Function.withreturn)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS = $"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(SystemReflect.Function.withargs)}";

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

    */
    #endregion
}