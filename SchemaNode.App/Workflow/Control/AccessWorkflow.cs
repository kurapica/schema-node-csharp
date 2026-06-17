using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

/// <summary>
/// Sets the access information for the workflow, which can be used by the subsequent workflow to determine the access control.
/// </summary>
[Schema($"{NS_SYSTEM_WORKFLOW_CONTROL}.access")]
public class AccessWorkflow : Workflow
{
    public Task ProcessAsync(WorkflowContext context,
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_APP)] string app, 
        [Schema(NS_SYSTEM_SCHEMA_DOMAIN_TARGET)] string target)
    {
        context.Done(this, access: new Access { App = app, Target = target });
        return Task.CompletedTask;
    }
}
