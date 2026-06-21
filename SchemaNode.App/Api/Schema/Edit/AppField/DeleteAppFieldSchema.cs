using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema.Provider;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The DeleteAppFieldSchema api
/// </summary>
public class DeleteAppFieldSchemaApi : SchemaApi<DeleteAppFieldSchemaRequest, DeleteAppFieldSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<DeleteAppFieldSchemaResponse?> ExecuteAsync(DeleteAppFieldSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]DeleteAppFieldSchema [Request]{request}", request);

        return new DeleteAppFieldSchemaResponse
        {
            Result = await SchemaContext.DeleteAppFieldSchemaAsync(request.App, request.Field)
        };
    }
}

/// <summary>
/// The DeleteAppFieldSchema request
/// </summary>
public class DeleteAppFieldSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// the app name
    /// </summary>
    [Required]
    public required string App { get; set; }

    /// <summary>
    /// The app field schema
    /// </summary>
    [Required]
    public required string Field { get; set; }
}

/// <summary>
/// The DeleteAppFieldSchema response
/// </summary>
public class DeleteAppFieldSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}