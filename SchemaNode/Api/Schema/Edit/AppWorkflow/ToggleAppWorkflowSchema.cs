using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Http;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The ToggleAppWorkflowSchema api
/// </summary>
public class ToggleAppWorkflowSchemaApi : SchemaApi<ToggleAppWorkflowSchemaRequest, ToggleAppWorkflowSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<ToggleAppWorkflowSchemaResponse?> ExecuteAsync(ToggleAppWorkflowSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]ToggleAppWorkflowSchema [Request]{request}", request);

        await Task.Yield();

        return new ToggleAppWorkflowSchemaResponse
        {
            Result = await SchemaContext.ToggleAppWorkflowSchemaAsync(request.App, request.Workflow, request.Active)
        };
    }
}

/// <summary>
/// The ToggleAppWorkflowSchema request
/// </summary>
public class ToggleAppWorkflowSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// the app name
    /// </summary>
    [Required]
    public required string App { get; set; }

    /// <summary>
    /// The work flow name
    /// </summary>
    [Required]
    public required string Workflow { get; set; }
    
    /// <summary>
    /// Whether to activate or deactivate the workflow
    /// </summary>
    public bool Active { get; set; }
}

/// <summary>
/// The ToggleAppWorkflowSchema response
/// </summary>
public class ToggleAppWorkflowSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}