using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Application;

/// <summary>
/// The SetSourceTarget api
/// </summary>
public class SetSourceTargetApi : SchemaApi<SetSourceTargetRequest, SetSourceTargetResponse>
{
    /// <inheritdoc />
    protected override async Task<SetSourceTargetResponse?> ExecuteAsync(SetSourceTargetRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SetSourceTarget [Request]{request}", request);

        if (string.IsNullOrEmpty(request.Target)) throw new Exception(APP_TARGET_REQUIRED);

        AppType app = await SchemaContext.GetAppTypeAsync(request.App) ?? throw new Exception(APP_NOT_FOUND);
        AppFieldType field = app.Fields?.FirstOrDefault(f => f.SourceApp != null && f.SourceApp.Equals(request.SourceApp, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception(APP_FIELD_NOT_FOUND);

        // Authorize
        await SchemaContext.AuthorizeAsync(field, PolicyScope.DataWrite);
        
        // Set app access
        SchemaContext.SetAppAccess(request.App, request.Target, field.Name);

        await SchemaContext.SetSourceFieldNode(field, request.Target, request.SourceTarget);

        return new SetSourceTargetResponse
        {
            Result = true
        };
    }
}

/// <summary>
/// The SetSourceTarget request
/// </summary>
public class SetSourceTargetRequest : SchemaApiRequest
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
    /// The source app
    /// </summary>
    [Required]
    public required string SourceApp { get; set; }

    /// <summary>
    /// The source target
    /// </summary>
    public string SourceTarget { get; set; } = string.Empty;
}

/// <summary>
/// The SetSourceTarget response
/// </summary>
public class SetSourceTargetResponse : SchemaApiResponse
{
    /// <summary>
    /// The source target
    /// </summary>
    public bool Result { get; set; }
}
