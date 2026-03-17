using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// The workflow event
/// </summary>
[Schema($"{NS_SYSTEM_EVENT}.workflow")]
public class WorkflowEvent(WorkflowContext context): AppEvent(context.WorkflowType!.App)
{
    /// <summary>
    /// The topic
    /// </summary>
    public override string Topic => $"{base.Topic}/{context.WorkflowType!.Name}";
}