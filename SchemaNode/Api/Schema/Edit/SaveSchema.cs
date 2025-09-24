using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SaveSchema api
/// </summary>
public class SaveSchemaApi : SchemaApi<SaveSchemaRequest, SaveSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<SaveSchemaResponse?> ExecuteAsync(SaveSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SaveSchema [Request]{request}", request);
        
        return new SaveSchemaResponse
        {
            Result = await SchemaContext.SaveSchemaAsync(request.Schema)
        };
    }
}

/// <summary>
/// The SaveSchema request
/// </summary>
public class SaveSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The new schema
    /// </summary>
    [Required]
    public NodeSchema Schema { get; set; } = null!;
}

/// <summary>
/// The SaveSchema response
/// </summary>
public class SaveSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}