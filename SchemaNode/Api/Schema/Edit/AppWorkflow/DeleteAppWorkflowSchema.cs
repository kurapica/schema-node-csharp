using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The DeleteAppWorkflowSchema api
/// </summary>
public class DeleteAppWorkflowSchemaApi : SchemaApi<DeleteAppWorkflowSchemaRequest, DeleteAppWorkflowSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<DeleteAppWorkflowSchemaResponse?> ExecuteAsync(DeleteAppWorkflowSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]DeleteAppWorkflowSchema [Request]{request}", request);

        return new DeleteAppWorkflowSchemaResponse
        {
            Result = await SchemaContext.DeleteAppWorkflowSchemaAsync(request.App, request.Workflow)
        };
    }
}

/// <summary>
/// The DeleteAppWorkflowSchema request
/// </summary>
public class DeleteAppWorkflowSchemaRequest : SchemaApiRequest
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
}

/// <summary>
/// The DeleteAppWorkflowSchema response
/// </summary>
public class DeleteAppWorkflowSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}