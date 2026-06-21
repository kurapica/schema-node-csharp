using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Utility;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Info;

/// <summary>
/// The Authorize api
/// </summary>
public class AuthorizeApi : SchemaApi<AuthorizeRequest, AuthorizeResponse>
{
    /// <inheritdoc />
    protected override async Task<AuthorizeResponse?> ExecuteAsync(AuthorizeRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]Authorize [Request]{request}", request);

        // schema
        if (!string.IsNullOrEmpty(request.Name))
        {
            NodeType schema = await SchemaContext.GetNodeTypeAsync(request.Name)
                ?? throw new Exception(ErrorCodes.TYPE_NOT_FOUND);
            
            return new AuthorizeResponse
            {
                Result = await SchemaContext.AuthorizeAsync(schema, request.Scope, true)
            };
        }
        
        // app
        if (string.IsNullOrEmpty(request.App)) throw new Exception(AppErrorCodes.APP_NOT_FOUND);
        AppType app = await SchemaContext.GetAppTypeAsync(request.App) ?? throw new Exception(AppErrorCodes.APP_NOT_FOUND);

        // field
        if (!string.IsNullOrEmpty(request.Field))
        {
            var field = app.GetField(request.Field) ?? throw new Exception(AppErrorCodes.APP_FIELD_NOT_FOUND);
            return new AuthorizeResponse
            {
                Result = await SchemaContext.AuthorizeAsync(field, request.Scope, true)
            };
        }
        
        // workflow
        if (!string.IsNullOrEmpty(request.Workflow))
        {
            var workflow = app.GetWorkflow(request.Workflow) ?? throw new Exception(AppErrorCodes.APP_WORKFLOW_NOT_FOUND);
            return new AuthorizeResponse
            {
                Result = await SchemaContext.AuthorizeAsync(workflow, request.Scope, true)
            };
        }

        return new AuthorizeResponse
        {
            Result = await SchemaContext.AuthorizeAsync(app, request.Scope, true)
        };
    }
}

/// <summary>
/// The Authorize request
/// </summary>
public class AuthorizeRequest : SchemaApiRequest
{
    public string? Name { get; set; }
    
    /// <summary>
    /// The application
    /// </summary>
    public string? App { get; set; }
    
    /// <summary>
    /// The app field
    /// </summary>
    public string? Field { get; set; }
    
    /// <summary>
    /// The workflow name
    /// </summary>
    public string? Workflow { get; set; }
    
    /// <summary>
    /// The policy scope
    /// </summary>
    [Required]
    public PolicyScope Scope { get; set; }
}

/// <summary>
/// The Authorize response
/// </summary>
public class AuthorizeResponse : SchemaApiResponse
{
    /// <summary>
    /// Whether the authorization is successful
    /// </summary>
    public bool Result { get; set; }
}