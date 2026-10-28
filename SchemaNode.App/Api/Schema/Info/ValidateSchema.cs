using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Schema;
using SchemaNode.Schema.Validation;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// Validates a candidate node schema without saving it.
/// <para>
/// This is the server side validation entry used by the AI chat UI (and any other client) before
/// a candidate is applied to the editor. It uses the same <see cref="SchemaCandidateValidator"/>
/// as the chat agent and the MCP validation tool.
/// </para>
/// </summary>
public class ValidateSchemaApi : SchemaApi<ValidateSchemaRequest, ValidateSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<ValidateSchemaResponse?> ExecuteAsync(
        ValidateSchemaRequest request, CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]ValidateSchema [Request]{request}", request);

        SchemaValidationResult result = await SchemaContext.ValidateNodeSchemaAsync(
            request.Schema,
            request.TargetName,
            checkAuthorization: true,
            cancellationToken);

        return new ValidateSchemaResponse
        {
            IsValid = result.IsValid,
            Errors = result.Errors.ToArray(),
            Warnings = result.Warnings.ToArray(),
            Operation = result.Operation,
            TargetName = result.TargetName,
            Schema = result.Schema,
            Normalized = result.Normalized,
        };
    }
}

/// <summary>
/// The ValidateSchema request.
/// </summary>
public class ValidateSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The candidate node schema, in the <c>system.schema.node.schema</c> format.
    /// </summary>
    [Required]
    public JsonNode? Schema { get; set; }

    /// <summary>
    /// The full name of the schema currently edited. When set, the candidate must keep this identity.
    /// </summary>
    public string? TargetName { get; set; }
}

/// <summary>
/// The ValidateSchema response.
/// </summary>
public class ValidateSchemaResponse : SchemaApiResponse
{
    /// <summary>Whether the candidate is valid.</summary>
    public bool IsValid { get; set; }

    /// <summary>The blocking validation problems.</summary>
    public SchemaValidationError[] Errors { get; set; } = [];

    /// <summary>Non blocking findings.</summary>
    public string[] Warnings { get; set; } = [];

    /// <summary><c>create</c> or <c>update</c>.</summary>
    public string? Operation { get; set; }

    /// <summary>The candidate full name.</summary>
    public string? TargetName { get; set; }

    /// <summary>The parsed node schema.</summary>
    public NodeSchema? Schema { get; set; }

    /// <summary>The normalized schema JSON.</summary>
    public JsonNode? Normalized { get; set; }
}
