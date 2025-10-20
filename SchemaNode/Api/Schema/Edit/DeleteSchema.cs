using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The DeleteSchema api
/// </summary>
public class DeleteSchemaApi : SchemaApi<DeleteSchemaRequest, DeleteSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<DeleteSchemaResponse?> ExecuteAsync(DeleteSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]DeleteSchema [Request]{request}", request);

        return new DeleteSchemaResponse
        {
            Result = await SchemaContext.DeleteSchemaAsync(request.Name)
        };
    }
}

/// <summary>
/// The DeleteSchema request
/// </summary>
public class DeleteSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The schema name
    /// </summary>
    [Required]
    public string Name { get; set; } = null!;
}

/// <summary>
/// The DeleteSchema response
/// </summary>
public class DeleteSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}