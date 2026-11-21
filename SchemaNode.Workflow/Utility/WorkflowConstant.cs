using static SchemaNode.Utility.Constant;

// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// The constants for the workflow module
/// </summary>
public static class WorkflowConstant
{
    #region Schema Kind

    public const string SCHEMA_KIND_NODE_EVENT = "node.event";
    public const string SCHEMA_KIND_NODE_WORKFLOW = "node.workflow";
    public const string SCHEMA_KIND_NODE_WORKFLOW_NODE = "node.workflow.node";
    public const string SCHEMA_KIND_NODE_WORKFLOW_STEP = "node.workflow.step";

    public const string NODE_KIND_EVENT = "event";
    public const string NODE_KIND_WORKFLOW = "workflow";
    public const string NODE_KIND_WORKFLOW_NODE = "workflow.node";

    public const int SCHEMA_KIND_ORDER_EVENT = 20;
    public const int SCHEMA_KIND_ORDER_WORKFLOW_NODE = 21;
    public const int SCHEMA_KIND_ORDER_WORKFLOW = 22;
    public const int SCHEMA_KIND_ORDER_WORKFLOW_STEP = 23;

    #endregion

    #region Namespace

    public const string NS_SYSTEM_EVENT = "system.event";
    public const string NS_SYSTEM_WORKFLOW = "system.workflow";
    public const string NS_SYSTEM_WORKFLOW_CRON = $"{NS_SYSTEM_WORKFLOW}.cron";
    public const string NS_SYSTEM_WORKFLOW_NODE = $"{NS_SYSTEM_WORKFLOW}.node";
    public const string NS_SYSTEM_WORKFLOW_CONTROL = $"{NS_SYSTEM_WORKFLOW}.control";
    public const string NS_SYSTEM_WORKFLOW_INTERACTION = $"{NS_SYSTEM_WORKFLOW}.interaction";
    public const string NS_SYSTEM_WORKFLOW_EVENT = $"{NS_SYSTEM_WORKFLOW}.event";

    public const string NS_SYSTEM_SCHEMA_EVENT = $"{NS_SYSTEM_SCHEMA}.event";
    public const string NS_SYSTEM_SCHEMA_WORKFLOW = $"{NS_SYSTEM_SCHEMA}.workflow";
    public const string NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW = $"{NS_SYSTEM_SCHEMA_REFLECT}.workflow";
    public const string NS_SYSTEM_SCHEMA_REFLECT_EVENT = $"{NS_SYSTEM_SCHEMA_REFLECT}.event";

    #endregion

    #region Workflow kind

    public const string WORKFLOW_KIND_WORKFLOW = "workflow";
    public const string WORKFLOW_KIND_CALL = "call";
    public const string WORKFLOW_KIND_EVENT = "event";
    public const string WORKFLOW_KIND_INTERACTION = "interaction";

    #endregion

    #region Topic

    public const char TOPIC_SEP = '/';
    public const string TOPIC_WILDCARD_SINGLE = "+";
    public const string TOPIC_WILDCARD_MULTI = "*";
    public const string TOPIC_WILDCARD_ALL = "#";

    #endregion
}
