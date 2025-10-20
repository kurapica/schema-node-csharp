using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SaveAppSchema api
/// </summary>
public class SaveAppSchemaApi : SchemaApi<SaveAppSchemaRequest, SaveAppSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<SaveAppSchemaResponse?> ExecuteAsync(SaveAppSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SaveAppSchema [Request]{request}", request);

        return new SaveAppSchemaResponse
        {
            Result = await SchemaContext.SaveAppSchemaAsync(request.Schema)
        };
    }
}

/// <summary>
/// The SaveAppSchema request
/// </summary>
public class SaveAppSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The app schema
    /// </summary>
    [Required]
    public required AppSchema Schema { get; set; }
}

/// <summary>
/// The SaveAppSchema response
/// </summary>
public class SaveAppSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}