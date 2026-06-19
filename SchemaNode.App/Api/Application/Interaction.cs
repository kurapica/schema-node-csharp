using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Node;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using SchemaNode.Workflow;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Application;

/// <summary>
/// The Interaction api
/// </summary>
public class InteractionApi : SchemaApi<InteractionRequest, InteractionResponse>
{
    /// <inheritdoc />
    protected override async Task<InteractionResponse?> ExecuteAsync(InteractionRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]Interaction [Request]{request}", request);

        // Done
        return new InteractionResponse { WorkflowId = await SchemaContext.InteractionAsync(request) };
    }
}

public static class InteractionExtensions
{
    /// <summary>
    /// Process the interaction request
    /// </summary>
    public static async Task<Guid?> InteractionAsync(this SchemaContext context, InteractionRequest request, bool innerCall = false)
    {
        // Indicate the workflow node
        AppType app = await context.GetAppTypeAsync(request.App) ?? throw new Exception(AppErrorCodes.APP_NOT_FOUND);
        AppWorkflowType workflowType = app.GetWorkflow(request.Workflow) ?? throw new Exception(AppErrorCodes.APP_WORKFLOW_NOT_FOUND);
        if (workflowType.RootWorkflowContext == null) throw new Exception(AppErrorCodes.APP_WORKFLOW_NOT_START);
        
        // set access
        context.SetAccess(app.Name, request.Target);
        
        // authorize
        if (!innerCall)
            await context.AuthorizeAsync(workflowType, PolicyScope.FuncExecute);
        
        // Find the contains node
        BaseWorkflow node = (string.IsNullOrEmpty(request.Node) 
            ? workflowType.RootWorkflowContext.EntryWorkflow
            : workflowType.RootWorkflowContext.EntryWorkflow?.FindByName(request.Node))
            ?? throw new Exception(AppErrorCodes.APP_WORKFLOW_NODE_NOT_FOUND);

        // build the payload
        StructNode payload = (node.PayloadType as StructType)?.From(null) as StructNode
                                 ?? throw new Exception(WORKFLOW_NODE_PAYLOAD_TYPE_NOT_VALID);
        payload[nameof(InteractionPayload.App)] = request.App;
        payload[nameof(InteractionPayload.Target)] = request.Target;
        payload[nameof(InteractionRequest.Data)] = request.Data; // placeholder for Data
        
        // Start a new workflow
        if (request.WorkflowId == null)
        {
            // If terminate requested, just return
            if (request.Terminate == true) return null;
            if (node is FormWorkflow && (request.Data == null || request.Data.IsEmpty())) return null;
            
            if (!workflowType.Nodes[0].Name.Equals(node.Name, StringComparison.InvariantCultureIgnoreCase))
                throw new Exception(AppErrorCodes.APP_WORKFLOW_NODE_NOT_FOUND);
            
            // Start a new workflow
            return workflowType.RootWorkflowContext.Done(node.Name, payload)?.Id;
        }
        
        // Continue an existing workflow
        WorkflowContext workContext = workflowType.RootWorkflowContext.GetForkedWorkflowContextById(request.WorkflowId.Value)
            ?? throw new Exception(AppErrorCodes.APP_WORKFLOW_NOT_FOUND);
        
        // Check if still working
        if (request.Terminate == true)
        {
            await workContext.TerminateAsync();
        }
        else
        {
            WorkflowStatus status = workContext.GetWorkflowStatus(node.Name);
            if (status != WorkflowStatus.Running) throw new Exception(AppErrorCodes.APP_WORKFLOW_NODE_NOT_RUNNING);
            workContext.Done(node.Name, payload);
        }

        return null;
    }
}

/// <summary>
/// The Interaction request
/// </summary>
public class InteractionRequest : SchemaApiRequest
{
    /// <summary>
    /// The application
    /// </summary>
    [Required]
    public required string App { get; set; }
    
    /// <summary>
    /// The target
    /// </summary>
    [Required]
    public required string Target { get; set; }
    
    /// <summary>
    /// The workflow name
    /// </summary>
    [Required]
    public required string Workflow { get; set; }
    
    /// <summary>
    /// The workflow node name
    /// </summary>
    public string? Node { get; set; }
    
    /// <summary>
    /// If the start node is not the first node, the workflow id should be provided
    /// </summary>
    public Guid? WorkflowId { get; set; }
    
    /// <summary>
    /// The data
    /// </summary>
    public JsonNode? Data { get; set; }
    
    /// <summary>
    /// Terminate the workflow context with the given id
    /// </summary>
    public bool? Terminate { get; set; }
}

/// <summary>
/// The Interaction response
/// </summary>
public class InteractionResponse : SchemaApiResponse
{
    /// <summary>
    /// The workflow id
    /// </summary>
    public Guid? WorkflowId { get; set; }
}