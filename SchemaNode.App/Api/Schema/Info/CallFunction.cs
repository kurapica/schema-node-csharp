using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Property.App;
using SchemaNode.Property.Function;
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

        // get function node
        NodeType? node = await SchemaContext.GetNodeTypeAsync(request.Name);
        if (node is not FunctionType func || func.HasFlag<WorkflowOnly>() == true) return new CallFunctionResponse { Result = null };

        // set target
        if (!string.IsNullOrWhiteSpace(request.Target))
            SchemaContext.SetAccess(null, request.Target);

        // authorize
        await SchemaContext.AuthorizeAsync(node, PolicyScope.FuncExecute);
        
        // call function
        return new CallFunctionResponse
        {
            Result = await SchemaContext.CallFunctionAsync(func, request.Args, request.Return, request.Target)
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
    public string? Return { get; set; }
    
    /// <summary>
    /// The related target
    /// </summary>
    public string? Target { get; set; }
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