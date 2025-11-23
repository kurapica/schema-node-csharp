using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Api.Schema.Application;

/// <summary>
/// The GetSourceTarget api
/// </summary>
public class GetSourceTargetApi : SchemaApi<GetSourceTargetRequest, GetSourceTargetResponse>
{
    /// <inheritdoc />
    protected override async Task<GetSourceTargetResponse?> ExecuteAsync(GetSourceTargetRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]GetSourceTarget [Request]{request}", request);

        if (string.IsNullOrEmpty(request.Target)) throw new Exception(APP_TARGET_REQUIRED);

        AppType app = await SchemaContext.GetAppTypeAsync(request.App) ?? throw new Exception(APP_NOT_FOUND);
        AppFieldType field = app.Fields?.FirstOrDefault(f => f.SourceApp != null && f.SourceApp.Equals(request.SourceApp, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception(APP_FIELD_NOT_FOUND);

        // authorize
        await SchemaContext.AuthorizeAsync(field, PolicyScope.SchemaRead);
        
        // Set app access
        SchemaContext.SetAppAccess(field.App, request.Target, field.Name);
        
        var result = await SchemaContext.GetSourceFieldNode(field, request.Target, true);

        return new GetSourceTargetResponse
        {
            Target = result.Item2,
        };
    }
}

/// <summary>
/// The GetSourceTarget request
/// </summary>
public class GetSourceTargetRequest : SchemaApiRequest
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
}

/// <summary>
/// The GetSourceTarget response
/// </summary>
public class GetSourceTargetResponse : SchemaApiResponse
{
    /// <summary>
    /// The source target
    /// </summary>
    public string? Target { get; set; }
}
