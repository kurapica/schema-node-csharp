using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.WorkflowConstant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// Sets the access information for the workflow, which can be used by the subsequent workflow to determine the access control.
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.access")]
[Meta<OfNodeKind>(NODE_KIND_WORKFLOW_NODE)]
public class AccessWorkflow : BaseWorkflow
{
    public Task ProcessAsync(WorkflowContext context,
        [Meta<SchemaType>(typeof(AppType))] string app, 
        string target)
    {
        context.SetAccess(new Access { App = app, Target = target });
        context.Done(this);
        return Task.CompletedTask;
    }
}
