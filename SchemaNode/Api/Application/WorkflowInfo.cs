using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Components;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Application;

/// <summary>
/// The WorkflowInfo api
/// </summary>
public class WorkflowInfoApi : SchemaApi<WorkflowInfoRequest, WorkflowInfoResponse>
{
    /// <inheritdoc />
    protected override async Task<WorkflowInfoResponse?> ExecuteAsync(WorkflowInfoRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]WorkflowInfo [Request]{request}", request);

        return new WorkflowInfoResponse
        {
            Status = await SchemaContext.GetWorkflowInfo(request)
        };
    }
}

/// <summary>
/// The WorkflowInfo extensions
/// </summary>
public static class WorkflowInfoExtensions
{
    /// <summary>
    /// Process the interaction request
    /// </summary>
    public static async Task<WorkflowStatus?> GetWorkflowInfo(this SchemaContext context, WorkflowInfoRequest request)
    {
        // Indicate the workflow node
        AppType app = await context.GetAppTypeAsync(request.App) ?? throw new Exception(APP_NOT_FOUND);
        AppWorkflowType workflowType = app.GetWorkflow(request.Workflow) ?? throw new Exception(WORKFLOW_NOT_FOUND);
        if (workflowType.RootWorkflowContext == null) throw new Exception(WORKFLOW_NOT_START);
        
        // authorize
        await context.AuthorizeAsync(workflowType, PolicyScope.DataRead);
        
        // Continue an existing workflow
        WorkflowContext? workContext = workflowType.RootWorkflowContext.GetForkedWorkflowContextById(request.WorkflowId);
        return workContext?.GetWorkflowStatus() ?? WorkflowStatus.Terminated;
    }
}

/// <summary>
/// The WorkflowInfo request
/// </summary>
public class WorkflowInfoRequest : SchemaApiRequest
{
    /// <summary>
    /// The application
    /// </summary>
    [Required]
    public required string App { get; set; }
    
    /// <summary>
    /// The workflow name
    /// </summary>
    [Required]
    public required string Workflow { get; set; }
    
    /// <summary>
    /// If the start node is not the first node, the workflow id should be provided
    /// </summary>
    [Required]
    public Guid WorkflowId { get; set; }
}

/// <summary>
/// The WorkflowInfo response
/// </summary>
public class WorkflowInfoResponse : SchemaApiResponse
{
    /// <summary>
    /// The workflow status
    /// </summary>
    public WorkflowStatus? Status { get; set; }
}