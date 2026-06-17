using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// The constant
/// </summary>
public static class AppConstant
{
    public const string SCHEMA_KIND_APP = "app";
    public const string SCHEMA_KIND_APP_FIELD = "appfield";
    public const string SCHEMA_KIND_APP_WORKFLOW = "appworkflow";
    
    public const string SCHEMA_KIND_EVENT = "event";
    public const string SCHEMA_KIND_WORKFLOW = "workflow";

    internal const int SCHEMA_KIND_ORDER_APP = 20;
    internal const int SCHEMA_KIND_ORDER_APP_FIELD = 21;
    internal const int SCHEMA_KIND_ORDER_APP_WORKFLOW = 22;
    internal const int SCHEMA_KIND_ORDER_EVENT = 23;
    internal const int SCHEMA_KIND_ORDER_WORKFLOW = 24;

    internal const string NS_SYSTEM_EVENT = "system.event";
    internal const string NS_SYSTEM_WORKFLOW = "system.workflow";


    internal const string NS_SYSTEM_SCHEMA_APP = $"{NS_SYSTEM_SCHEMA}.{SCHEMA_KIND_APP}";
    internal const string NS_SYSTEM_SCHEMA_APP_FIELD = $"{NS_SYSTEM_SCHEMA_APP}.field";
    internal const string NS_SYSTEM_SCHEMA_APP_WORKFLOW = $"{NS_SYSTEM_SCHEMA_APP}.workflow";
    internal const string NS_SYSTEM_SCHEMA_EVENT = $"{NS_SYSTEM_SCHEMA}.{SCHEMA_KIND_EVENT}";
    internal const string NS_SYSTEM_SCHEMA_WORKFLOW = $"{NS_SYSTEM_SCHEMA}.{SCHEMA_KIND_WORKFLOW}";
    
    // workflow kind
    internal const string WORKFLOW_KIND_WORKFLOW = "workflow";
    internal const string WORKFLOW_KIND_CALL = "call";
    internal const string WORKFLOW_KIND_EVENT = "event";
    internal const string WORKFLOW_KIND_INTERACTION = "interaction";
    
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

    #region Expression Priority

    public const int EXP_DATA_SOURCE_PRIORITY = 60;

    #endregion
}