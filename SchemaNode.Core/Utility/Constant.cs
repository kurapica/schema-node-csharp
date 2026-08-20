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

    public const string NODE_SELF = "$self";
    public const string ARRAY_PREVIOUS = "$prev";
    public const string ARRAY_ELEMENT = "$ele";
    public const string NODE_PARENT = "$parent";
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
    public const string NS_SYSTEM_CONTEXT = "system.context";

    // language x translate x entry
    public const string NS_SYSTEM_LANGUAGE = "system.language";
    public const string NS_SYSTEM_LOCALE_STRING = "system.localestring";
    public const string NS_SYSTEM_LOCALE_TRAN = "system.localetran";

    // entry for white list
    public const string NS_SYSTEM_ENTRY = "system.entry";
    public const string NS_SYSTEM_ENTRYS = $"{NS_SYSTEM_ENTRY}s";
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
    public const string NS_SYSTEM_STR = "system.str";

    public const string NS_SYSTEM_SCHEMA_REFLECT = $"{NS_SYSTEM_SCHEMA}.reflect";
    public const string NS_SYSTEM_SCHEMA_REFLECT_TYPE = $"{NS_SYSTEM_SCHEMA_REFLECT}.type";
    public const string NS_SYSTEM_SCHEMA_REFLECT_FUNC = $"{NS_SYSTEM_SCHEMA_REFLECT}.func";
    public const string NS_SYSTEM_SCHEMA_REFLECT_ARRAY = $"{NS_SYSTEM_SCHEMA_REFLECT}.array";
    public const string NS_SYSTEM_SCHEMA_REFLECT_ENUM = $"{NS_SYSTEM_SCHEMA_REFLECT}.enum";
    public const string NS_SYSTEM_SCHEMA_REFLECT_STRUCT = $"{NS_SYSTEM_SCHEMA_REFLECT}.struct";
    public const string NS_SYSTEM_SCHEMA_REFLECT_PROPERTY = $"{NS_SYSTEM_SCHEMA_REFLECT}.prop";
    public const string NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND = $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.isschemakind)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND = $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.isvaluekind)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_IS_ARRAY_ELE = $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(Function.Reflect.Array.isarrayele)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_GET_ACCESS_ENTRIES = $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.getaccessentries)}";

    public const string NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN = $"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(Function.Reflect.Function.withreturn)}";
    public const string NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS = $"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.{nameof(Function.Reflect.Function.withargs)}";

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
}