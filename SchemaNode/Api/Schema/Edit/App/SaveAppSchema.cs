using Microsoft.Extensions.Logging;
using SchemaNode.Components;
using SchemaNode.Http;
using SchemaNode.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SaveAppSchema api
/// </summary>
public class SaveAppSchemaApi : SchemaApi<SaveAppSchemaRequest, SaveAppSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<SaveAppSchemaResponse?> ExecuteAsync(SaveAppSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SaveAppSchema [Request]{request}", request);

        return new SaveAppSchemaResponse
        {
            Result = await SchemaContext.SaveAppSchemaAsync(request.Schema)
        };
    }
}

/// <summary>
/// The SaveAppSchema request
/// </summary>
public class SaveAppSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The app schema
    /// </summary>
    [Required]
    public required AppSchema Schema { get; set; }
}

/// <summary>
/// The SaveAppSchema response
/// </summary>
public class SaveAppSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}

/// <summary>
/// The app schema data
/// </summary>
public class AppSchemaData
{
    /// <summary>
    /// The application name
    /// </summary>
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The display name
    /// </summary>
    public LocaleString? Display { get; set; }
    
    /// <summary>
    /// The description
    /// </summary>
    public LocaleString? Desc { get; set; }
    
    /// <summary>
    /// The authentication policy type
    /// </summary>
    public string? Auth { get; set; }

    /// <summary>
    /// The app authentication policy type
    /// </summary>
    public PolicyItem[]? Auths { get; set; }
    
    /// <summary>
    /// The application field relations
    /// </summary>
    public StructRelationSchema[]? Relations { get; set; }

    /// <summary>
    /// The extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    public static implicit operator AppSchema(AppSchemaData data)
    {
        return new AppSchema
        {
            Name = data.Name,
            Display = data.Display,
            Auth = data.Auth,
            Auths = data.Auths,
            Relations = data.Relations,
            Extensions = data.Extensions,
        };
    }
}
