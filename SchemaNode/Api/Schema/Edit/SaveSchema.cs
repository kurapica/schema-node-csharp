using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SchemaNode.Enum;
using SchemaNode.Http;
using SchemaNode.Schema;

namespace SchemaNode.Api.Schema.Edit;

/// <summary>
/// The SaveSchema api
/// </summary>
public class SaveSchemaApi : SchemaApi<SaveSchemaRequest, SaveSchemaResponse>
{
    /// <inheritdoc />
    protected override async Task<SaveSchemaResponse?> ExecuteAsync(SaveSchemaRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("[Api]SaveSchema [Request]{request}", request);
        
        return new SaveSchemaResponse
        {
            Result = await SchemaContext.SaveSchemaAsync(request.Schema)
        };
    }
}

/// <summary>
/// The SaveSchema request
/// </summary>
public class SaveSchemaRequest : SchemaApiRequest
{
    /// <summary>
    /// The new schema
    /// </summary>
    [Required]
    public NodeSchemaData Schema { get; set; } = null!;
}

/// <summary>
/// The SaveSchema response
/// </summary>
public class SaveSchemaResponse : SchemaApiResponse
{
    /// <summary>
    /// The result
    /// </summary>
    public bool Result { get; set; }
}

public class NodeSchemaData
{
    /// <summary>
    /// The schema name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The schema type
    /// </summary>
    public SchemaType Type { get; set; } = SchemaType.Namespace;

    /// <summary>
    /// The schema display
    /// </summary>
    public LocaleString? Display { get; set; }

    /// <summary>
    /// The scalar schema if type is scalar
    /// </summary>
    public ScalarSchema? Scalar { get; set; }

    /// <summary>
    /// The enum schema if type is enum
    /// </summary>
    public EnumSchema? Enum  { get; set; }

    /// <summary>
    /// The struct schema if type is struct
    /// </summary>
    public StructSchema? Struct { get; set; }

    /// <summary>
    /// The array schema if type is array
    /// </summary>
    public ArraySchema? Array  { get; set; }

    /// <summary>
    /// The function schema if type is function
    /// </summary>
    public FunctionSchema? Func { get; set; }

    #region Conversion

    public static implicit operator NodeSchema(NodeSchemaData schema)
    {
        return new NodeSchema
        {
            Name = schema.Name,
            Type = schema.Type,
            Display = schema.Display,
            Scalar = schema.Type == SchemaType.Scalar ? schema.Scalar : null,
            Enum = schema.Type == SchemaType.Enum ? schema.Enum : null,
            Struct = schema.Type == SchemaType.Struct ? schema.Struct : null,
            Array = schema.Type == SchemaType.Array ? schema.Array : null,
            Func = schema.Type == SchemaType.Function ? schema.Func : null,
            LoadState = SchemaLoadState.Server
        };
    }

    #endregion
}