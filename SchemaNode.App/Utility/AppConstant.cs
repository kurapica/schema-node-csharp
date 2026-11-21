using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// The constant
/// </summary>
public static class AppConstant
{
    public const string APP_PROVIDER = "$app";
    public const string SCHEMA_KIND_APP = "app";
    public const string SCHEMA_KIND_APP_FIELD = "app.field";
    public const string SCHEMA_KIND_APP_WORKFLOW = "app.workflow";
    public const string SCHEMA_KIND_APP_WORKFLOW_STEP = "app.workflow.step";

    public const int SCHEMA_KIND_ORDER_APP = 30;
    public const int SCHEMA_KIND_ORDER_APP_FIELD = 31;
    public const int SCHEMA_KIND_ORDER_APP_WORKFLOW = 32;
    public const int SCHEMA_KIND_ORDER_APP_WORKFLOW_STEP = 33;

    public const string NS_SYSTEM_DATA_APP = $"{NS_SYSTEM_DATA}.app";

    public const string NS_SYSTEM_SCHEMA_APP = $"{NS_SYSTEM_SCHEMA}.{SCHEMA_KIND_APP}";
    public const string NS_SYSTEM_SCHEMA_APP_FIELD = $"{NS_SYSTEM_SCHEMA_APP}.field";
    public const string NS_SYSTEM_SCHEMA_APP_WORKFLOW = $"{NS_SYSTEM_SCHEMA_APP}.workflow";
    public const string NS_SYSTEM_SCHEMA_REFLECT_APP = $"{NS_SYSTEM_SCHEMA_REFLECT}.app";

    public const string NS_SYSTEM_SCHEMA_PROPERTY_APP = $"{NS_SYSTEM_SCHEMA_PROP}.app";

    #region Workflow module compatibility

    public const string SCHEMA_KIND_NODE_WORKFLOW_STEP = WorkflowConstant.SCHEMA_KIND_NODE_WORKFLOW_STEP;
    public const string SCHEMA_KIND_NODE_EVENT = WorkflowConstant.SCHEMA_KIND_NODE_EVENT;
    public const string SCHEMA_KIND_NODE_WORKFLOW = WorkflowConstant.SCHEMA_KIND_NODE_WORKFLOW;
    public const string SCHEMA_KIND_NODE_WORKFLOW_NODE = WorkflowConstant.SCHEMA_KIND_NODE_WORKFLOW_NODE;
    public const string NODE_KIND_EVENT = WorkflowConstant.NODE_KIND_EVENT;
    public const string NODE_KIND_WORKFLOW = WorkflowConstant.NODE_KIND_WORKFLOW;

    public const int SCHEMA_KIND_ORDER_EVENT = WorkflowConstant.SCHEMA_KIND_ORDER_EVENT;
    public const int SCHEMA_KIND_ORDER_WORKFLOW = WorkflowConstant.SCHEMA_KIND_ORDER_WORKFLOW;
    public const int SCHEMA_KIND_ORDER_WORKFLOW_NODE = WorkflowConstant.SCHEMA_KIND_ORDER_WORKFLOW_NODE;
    public const int SCHEMA_KIND_ORDER_WORKFLOW_STEP = WorkflowConstant.SCHEMA_KIND_ORDER_WORKFLOW_STEP;

    public const string NS_SYSTEM_EVENT = WorkflowConstant.NS_SYSTEM_EVENT;
    public const string NS_SYSTEM_WORKFLOW = WorkflowConstant.NS_SYSTEM_WORKFLOW;
    public const string NS_SYSTEM_WORKFLOW_CRON = WorkflowConstant.NS_SYSTEM_WORKFLOW_CRON;
    public const string NS_SYSTEM_WORKFLOW_NODE = WorkflowConstant.NS_SYSTEM_WORKFLOW_NODE;
    public const string NS_SYSTEM_WORKFLOW_CONTROL = WorkflowConstant.NS_SYSTEM_WORKFLOW_CONTROL;
    public const string NS_SYSTEM_WORKFLOW_INTERACTION = WorkflowConstant.NS_SYSTEM_WORKFLOW_INTERACTION;
    public const string NS_SYSTEM_WORKFLOW_EVENT = WorkflowConstant.NS_SYSTEM_WORKFLOW_EVENT;

    public const string NS_SYSTEM_SCHEMA_EVENT = WorkflowConstant.NS_SYSTEM_SCHEMA_EVENT;
    public const string NS_SYSTEM_SCHEMA_WORKFLOW = WorkflowConstant.NS_SYSTEM_SCHEMA_WORKFLOW;
    public const string NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW = WorkflowConstant.NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW;
    public const string NS_SYSTEM_SCHEMA_REFLECT_EVENT = WorkflowConstant.NS_SYSTEM_SCHEMA_REFLECT_EVENT;

    public const string WORKFLOW_KIND_WORKFLOW = WorkflowConstant.WORKFLOW_KIND_WORKFLOW;
    public const string WORKFLOW_KIND_CALL = WorkflowConstant.WORKFLOW_KIND_CALL;
    public const string WORKFLOW_KIND_EVENT = WorkflowConstant.WORKFLOW_KIND_EVENT;
    public const string WORKFLOW_KIND_INTERACTION = WorkflowConstant.WORKFLOW_KIND_INTERACTION;

    public const char TOPIC_SEP = WorkflowConstant.TOPIC_SEP;
    public const string TOPIC_WILDCARD_SINGLE = WorkflowConstant.TOPIC_WILDCARD_SINGLE;
    public const string TOPIC_WILDCARD_MULTI = WorkflowConstant.TOPIC_WILDCARD_MULTI;
    public const string TOPIC_WILDCARD_ALL = WorkflowConstant.TOPIC_WILDCARD_ALL;

    #endregion

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
    /// The default root key
    /// </summary>
    public const string ROOT = "__root";
    
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