using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Data.Entity;

[Meta<App>(NS_SYSTEM_SCHEMA)]
[Meta<EnableStorage>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.entity.appworkflow")]
internal class AppWorkflowEntity
{

    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<UniqueIndex>("seqno",0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The field name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<UniqueIndex>("seqno", 1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The seqno
    /// </summary>
    [Meta<UniqueIndex>("seqno", 1)]
    public int Seqno { get; set; }

    /// <summary>
    /// Whether the workflow is active
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// The workflow nodes
    /// </summary>
    public JsonArray? Nodes { get; set; }

    /// <summary>
    /// The extension properties of the workflow
    /// </summary>
    public JsonObject? Extensions { get; set; }


    #region Conversion

    public static implicit operator AppWorkflowEntity?(AppWorkflowSchema? appWorkflowSchema)
    {
        if (appWorkflowSchema == null) return null;

        return new AppWorkflowEntity
        {
            App = appWorkflowSchema.App,
            Name = appWorkflowSchema.Name,
            Seqno = appWorkflowSchema.Seqno,
            Active = appWorkflowSchema.Active,
            Nodes = appWorkflowSchema.Nodes is { Length: > 0 }
                ? JsonSerializer.SerializeToNode(appWorkflowSchema.Nodes, NodeJsonOptions) as JsonArray
                : null,
            Extensions = appWorkflowSchema.Extensions?.DeepClone() as JsonObject
        };
    }

    public static implicit operator AppWorkflowSchema?(AppWorkflowEntity? appWorkflowEntity)
    {
        if (appWorkflowEntity == null) return null;

        return new AppWorkflowSchema
        {
            App = appWorkflowEntity.App,
            Name = appWorkflowEntity.Name,
            Seqno = appWorkflowEntity.Seqno,
            Active = appWorkflowEntity.Active,
            Nodes = appWorkflowEntity.Nodes?.Deserialize<AppWorkflowNodeSchema[]>(NodeJsonOptions) ?? [],
            Extensions = appWorkflowEntity.Extensions?.DeepClone() as JsonObject
        };
    }

    #endregion

    #region Options

    // keep the property naming stable for the schema nodes round-trip
    private static readonly JsonSerializerOptions NodeJsonOptions = new()
    {
        // use the PropertyOwner converter: the default STF JsonExtensionData handling on JsonObject
        // writes the extensions object without the property name, which produces invalid JSON
        Converters = { new PropertyOwnerConverterFactory() },
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #endregion
}
