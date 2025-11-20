using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
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
        return new InteractionResponse { Result = await SchemaContext.Interaction(request) };
    }
}

public static class InteractionExtensions
{
    /// <summary>
    /// Process the interaction request
    /// </summary>
    public static async Task<bool> Interaction(this SchemaContext context, InteractionRequest request)
    {
        // Indicate the workflow node
        AppType app = await context.GetAppTypeAsync(request.App) ?? throw new Exception(APP_NOT_FOUND);
        AppWorkflowType workflowType = app.Workflows?
            .FirstOrDefault(w => w.Name.Equals(request.Workflow, StringComparison.InvariantCultureIgnoreCase))
            ?? throw new Exception(WORKFLOW_NOT_FOUND);
        if (workflowType.RootWorkflowContext == null) throw new Exception(WORKFLOW_NOT_START);
        
        // authorize
        await context.AuthorizeAsync(app, PolicyScope.DataWrite);
        
        // Set app access
        context.SetAppAccess(request.App, request.Target);

        // build the payload
        InteractionWorkflowPayload payload = new()
        {
            App = request.App,
            Target = request.Target,
            Workflow = request.Workflow,
            Node = request.Node,
            WorkflowId = request.WorkflowId,
        };
        
        // Find the match node
        Workflow node = workflowType.RootWorkflowContext.EntryWorkflow?.FindByName(request.Node)
            ?? throw new Exception(WORKFLOW_NODE_NOT_FOUND);

        // Gets the payload type
        AnySchemeType dataType = ((node.PayloadType as StructType)?.Fields.FirstOrDefault(
                f => f.Name.Equals(nameof(InteractionWorkflowPayload.Data), StringComparison.OrdinalIgnoreCase))
            ?.TypeNode) ?? throw new Exception(WORKFLOW_NODE_PAYLOAD_TYPE_NOT_VALID);
        payload.Data = dataType.CreateNode(request.Data);
        
        // Start a new workflow
        if (request.WorkflowId == null)
        {
            if (!workflowType.Nodes[0].Name.Equals(request.Node, StringComparison.InvariantCultureIgnoreCase))
                throw new Exception(WORKFLOW_NODE_NOT_FOUND);
            
            // Start a new workflow
            workflowType.RootWorkflowContext.Done(request.Node, node.PayloadType.CreateNode(payload));
        }
        
        // Continue an existing workflow
        else
        {
            WorkflowContext workContext = workflowType.RootWorkflowContext.GetForkedWorkflowContextById(request.WorkflowId.Value)
                ?? throw new Exception(WORKFLOW_NOT_FOUND);
            
            // Check if still working
            WorkflowStatus status = workContext.GetWorkflowStatus(request.Node);
            if (status != WorkflowStatus.Running) throw new Exception(WORKFLOW_NODE_NOT_RUNNING);
            workContext.Done(request.Node, node.PayloadType.CreateNode(payload));
        }

        return true;
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
    [Required]
    public required string Node { get; set; }
    
    /// <summary>
    /// If the start node is not the first node, the workflow id should be provided
    /// </summary>
    public Guid? WorkflowId { get; set; }
    
    /// <summary>
    /// The data
    /// </summary>
    public JsonNode? Data { get; set; }
}

/// <summary>
/// The Interaction response
/// </summary>
public class InteractionResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}