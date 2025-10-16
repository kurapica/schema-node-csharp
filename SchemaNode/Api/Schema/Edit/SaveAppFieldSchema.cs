using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SaveAppFieldSchema api
/// </summary>
public class SaveAppFieldSchemaApi : SchemaApi<SaveAppFieldSchemaRequest, SaveAppFieldSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<SaveAppFieldSchemaResponse?> ExecuteAsync(SaveAppFieldSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SaveAppFieldSchema [Request]{request}", request);

        return new SaveAppFieldSchemaResponse
        {
            Result = await SchemaContext.SaveAppFieldSchemaAsync(request.App, request.Schema)
        };
    }
}

/// <summary>
/// The SaveAppFieldSchema request
/// </summary>
public class SaveAppFieldSchemaRequest : SchemaApiRequest
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
    public required AppFieldSchema Schema { get; set; }
}

/// <summary>
/// The SaveAppFieldSchema response
/// </summary>
public class SaveAppFieldSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}