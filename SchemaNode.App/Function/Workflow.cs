using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using SchemaNode.Property.Workflow;
using SchemaNode.Schema;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Function;


[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW)]
public static class SystemReflectWorkflow
{
    /// <summary>
    /// Checks the given workflow is of the given kind
    /// </summary>
    public static async Task<bool> iskind(SchemaContext context,
        [Meta<SchemaType>(typeof(WorkflowType))] string workflow,
        [Meta<SchemaType>(typeof(WorkflowKind))] string kind)
    {
        var workflowType = await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow);
        return kind.Equals(workflowType?.WorkflowKind, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The workflow type has arguments
    /// </summary>
    public static async Task<bool> hasargs(SchemaContext context,
        [Meta<SchemaType>(typeof(WorkflowType))] string workflow)
    {
        var workflowType = await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow);
        return workflowType?.Args?.Length > 0;
    }

    /// <summary>
    /// The workflow type is forkable
    /// </summary>
    public static async Task<bool> isforkable(SchemaContext context,
        [Meta<SchemaType>(typeof(WorkflowKind))] string workflow)
    {
        var workflowType = await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow);
        return workflowType?.GetProperty<Forkable>()?.GetValue<bool>() ?? false;
    }
}