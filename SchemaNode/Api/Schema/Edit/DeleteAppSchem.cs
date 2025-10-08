using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The DeleteAppSchema api
/// </summary>
public class DeleteAppSchemaApi : SchemaApi<DeleteAppSchemaRequest, DeleteAppSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<DeleteAppSchemaResponse?> ExecuteAsync(DeleteAppSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]DeleteAppSchema [Request]{request}", request);

        return new DeleteAppSchemaResponse
        {
            Result = await SchemaContext.DeleteAppSchemaAsync(request.Name)
        };
    }
}

/// <summary>
/// The DeleteAppSchema request
/// </summary>
public class DeleteAppSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The app schema name
    /// </summary>
    [Required]
    public required string App { get; set; }
}

/// <summary>
/// The DeleteAppSchema response
/// </summary>
public class DeleteAppSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}