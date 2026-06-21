using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema.Provider;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SwapAppFieldSchema api
/// </summary>
public class SwapAppFieldSchemaApi : SchemaApi<SwapAppFieldSchemaRequest, SwapAppFieldSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<SwapAppFieldSchemaResponse?> ExecuteAsync(SwapAppFieldSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SwapAppFieldSchema [Request]{request}", request);

        return new SwapAppFieldSchemaResponse
        {
            Result = await SchemaContext.SwapAppFieldSchemaAsync(request.App, request.Field, request.Other)
        };
    }
}

/// <summary>
/// The SwapAppFieldSchema request
/// </summary>
public class SwapAppFieldSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// the app name
    /// </summary>
    [Required]
    public required string App { get; set; }

    /// <summary>
    /// The app field
    /// </summary>
    [Required]
    public required string Field { get; set; }

    /// <summary>
    /// The other field to swap with
    /// </summary>
    [Required]
    public required string Other { get; set; }
}

/// <summary>
/// The SwapAppFieldSchema response
/// </summary>
public class SwapAppFieldSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}