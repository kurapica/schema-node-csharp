using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Http;
using SchemaNode.Runtime;

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The CallFunction api
/// </summary>
public class CallFunctionApi : SchemaApi<CallFunctionRequest, CallFunctionResponse>
{
    /// <inheritdoc />
    protected override async Task<CallFunctionResponse?> ExecuteAsync(CallFunctionRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]CallFunction [Request]{request}", request);

        AnySchemeType? node = await SchemaContext.GetSchemaNodeAsync(request.Name);
        
        return new CallFunctionResponse
        {
            Result = node is FunctionType func ? await SchemaContext.CallFunctionAsync(func, request.Args, request.Generic) : null
        };
    }
}

/// <summary>
/// The CallFunction request
/// </summary>
public class CallFunctionRequest : SchemaApiRequest
{
    /// <summary>
    /// The function schema name
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// The arguments
    /// </summary>
    [Required]
    public JsonArray Args { get; set; } = [];
    
    /// <summary>
    /// The generic types
    /// </summary>
    public string[] Generic { get; set; } = [];
}

/// <summary>
/// The CallFunction response
/// </summary>
public class CallFunctionResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public JsonNode? Result { get; set; }
}