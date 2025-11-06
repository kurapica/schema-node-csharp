using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Context;

/// <summary>
/// The workflow entry context
/// </summary>
public class EntryWorkflowContext(AppType app, IServiceScopeFactory scopeFactory, IWorkflowScheduler scheduler)
    : WorkflowContext(app, scopeFactory)
{
    private AppWorkflowNodeSchema[] _nodeSchemas = [];

    /// <summary>
    /// The workflow node is done
    /// </summary>
    public new void Done(Workflow workflow, AnySchemaNode? payload)
    {
        // Starts a new workflow for the next node
        WorkflowContext context = new WorkflowContext(Application, Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>());
        context.InitializeAsync(_nodeSchemas).GetAwaiter().GetResult();
        context.Done(workflow.Name, payload);
        scheduler.Schedule(context); // schedule the new workflow context
    }
    
    /// <summary>
    /// Only use first node as entry
    /// </summary>
    public new async Task InitializeAsync(AppWorkflowNodeSchema[] nodeSchemas)
    {
        if (nodeSchemas == null || nodeSchemas.Length == 0)
            throw new ArgumentException("Node schemas cannot be null or empty", nameof(nodeSchemas));
        
        _nodeSchemas = nodeSchemas;
        await base.InitializeAsync(nodeSchemas.Take(1).ToArray());
        Process(); // process the entry node immediately
    }
}