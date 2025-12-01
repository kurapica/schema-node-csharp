using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Runtime;
// ReSharper disable UnusedAutoPropertyAccessor.Global

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

        AnySchemeType? node = await SchemaContext.GetSchemaTypeAsync(request.Name);
        if (node is not FunctionType func)  return new  CallFunctionResponse { Result = null };
        
        // authorize
        await SchemaContext.AuthorizeAsync(node, PolicyScope.FuncExecute);
        
        return new CallFunctionResponse
        {
            Result = await SchemaContext.CallFunctionAsync(func, request.Args, request.Generic)
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