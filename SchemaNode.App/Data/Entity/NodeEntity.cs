using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Data.Entity;

[Meta<App>(NS_SYSTEM_SCHEMA)]
[Meta<ScopePolicy>(AppScopeType.SystemLevel)]
[Meta<EnableStorage>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.entity.node")]
internal class NodeEntity
{
    /// <summary>
    /// The namespace which includes the schema
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(NamespaceType))]
    public string? Namespace { get; set; }

    /// <summary>
    /// The schema name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The schema kind
    /// </summary>
    [Meta<SchemaType>(typeof(NodeSchemaKind))]
    public string Kind { get; set; } = null!;

    /// <summary>
    /// The extension properties of the node
    /// </summary>
    public JsonObject? Extensions { get; set; }

    #region Conversion

    public static implicit operator NodeEntity?(NodeSchema? nodeSchema)
    {
        if (nodeSchema == null) return null;
        JsonObject? extensions = null;
        if (nodeSchema.Extensions is { Count : > 0})
        {
            extensions = new JsonObject();
            foreach (var kvp in nodeSchema.Extensions)
            {
                extensions[kvp.Key] = kvp.Value.DeepClone();
            }
        }

        return new NodeEntity
        {
            Namespace = nodeSchema.Namespace,
            Name = nodeSchema.Name,
            Kind = nodeSchema.Kind,
            Extensions = extensions
        };
    }

    public static implicit operator NodeSchema?(NodeEntity? nodeEntity)
    {
        if (nodeEntity == null) return null;
        var nodeSchema = new NodeSchema
        {
            Namespace = nodeEntity.Namespace,
            Name = nodeEntity.Name,
            Kind = nodeEntity.Kind
        };
        if (nodeEntity.Extensions is { Count: > 0 })
        {
            nodeSchema.Extensions = [];
            foreach (var kvp in nodeEntity.Extensions)
            {
                if (kvp.Value != null && !kvp.Value.IsEmpty())
                    nodeSchema.Extensions[kvp.Key] = kvp.Value.DeepClone();
            }
        }
        return nodeSchema;
    }
    
    #endregion
}
