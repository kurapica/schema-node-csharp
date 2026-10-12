using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Runtime;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The CallFunction api
/// </summary>
public class ValidateApi : SchemaApi<ValidateRequest, ValidateResponse>
{
    /// <inheritdoc />
    protected override async Task<ValidateResponse?> ExecuteAsync(ValidateRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]CallFunction [Request]{request}", request);

        // get function node
        NodeType? node = await SchemaContext.GetNodeTypeAsync(request.Name);
        if (node is not Runtime.ValueType type ) return new ValidateResponse { IsValid = false, Error = JsonValue.Create("NOT_VALID_TYPE") };

        // set target
        if (!string.IsNullOrWhiteSpace(request.App) || !string.IsNullOrWhiteSpace(request.Target))
            SchemaContext.SetAccess(request.App, request.Target);

        var res = await type.ValidateValueAsync(SchemaContext, request.Data);

        // call function
        return new ValidateResponse
        {
            IsValid = res?.IsValid == true,
            Error = res?.IsValid == true ? null : (res?.Violated ?? JsonValue.Create("NOT_VALID_DATA"))
        };
    }
}

/// <summary>
/// The CallFunction request
/// </summary>
public class ValidateRequest : SchemaApiRequest
{
    /// <summary>
    /// The type name
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// The json data
    /// </summary>
    public JsonNode? Data { get; set; }

    /// <summary>
    /// The application
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// The related target
    /// </summary>
    public string? Target { get; set; }
}

/// <summary>
/// The CallFunction response
/// </summary>
public class ValidateResponse : SchemaApiResponse
{
    /// <summary>
    /// The data is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The error result
    /// </summary>
    public JsonNode? Error { get; set; }
}