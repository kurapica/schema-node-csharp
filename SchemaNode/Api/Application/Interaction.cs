using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;

namespace SchemaNode.Api.Schema.Application;

/// <summary>
/// The Interaction api
/// </summary>
public class InteractionApi : SchemaApi<InteractionRequest, InteractionResponse>
{
    /// <inheritdoc />
    protected override async Task<InteractionResponse?> ExecuteAsync(InteractionRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]Interaction [Request]{request}", request);

        await Task.Yield();

        return new InteractionResponse
        {
        };
    }
}

/// <summary>
/// The Interaction request
/// </summary>
public class InteractionRequest : SchemaApiRequest
{
    /// <summary>
    /// The application
    /// </summary>
    [Required]
    public required string App { get; set; }

    /// <summary>
    /// The target
    /// </summary>
    [Required]
    public required string Target { get; set; }

    /// <summary>
    /// The work flow id
    /// </summary>
    [Required]
    public Guid? Workflow { get; set; }
    
    /// <summary>
    /// The form type
    /// </summary>
    [Required]
    public string FormType { get; set; } = string.Empty;
    
    /// <summary>
    /// The form data
    /// </summary>
    public JsonNode? Form { get; set; }
}

/// <summary>
/// The Interaction response
/// </summary>
public class InteractionResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}