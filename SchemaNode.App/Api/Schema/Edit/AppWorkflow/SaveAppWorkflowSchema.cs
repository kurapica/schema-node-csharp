using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SaveAppWorkflowSchema api
/// </summary>
public class SaveAppWorkflowSchemaApi : SchemaApi<SaveAppWorkflowSchemaRequest, SaveAppWorkflowSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<SaveAppWorkflowSchemaResponse?> ExecuteAsync(SaveAppWorkflowSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SaveAppWorkflowSchema [Request]{request}", request);

        return new SaveAppWorkflowSchemaResponse
        {
            Result = await SchemaContext.SaveAppWorkflowSchemaAsync(request.App, request.Schema)
        };
    }
}

/// <summary>
/// The SaveAppWorkflowSchema request
/// </summary>
public class SaveAppWorkflowSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// the app name
    /// </summary>
    [Required]
    public required string App { get; set; }

    /// <summary>
    /// The work flow schema
    /// </summary>
    [Required]
    public required AppWorkflowSchema Schema { get; set; }
}

/// <summary>
/// The SaveAppWorkflowSchema response
/// </summary>
public class SaveAppWorkflowSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}