using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Property.Constraint;
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
        return new NodeEntity
        {
            Namespace = string.IsNullOrWhiteSpace(nodeSchema.Namespace) ? ROOT : nodeSchema.Namespace,
            Name = nodeSchema.Name,
            Kind = nodeSchema.Kind,
            Extensions = nodeSchema.Extensions?.DeepClone() as JsonObject
        };
    }

    public static implicit operator NodeSchema?(NodeEntity? nodeEntity)
    {
        if (nodeEntity == null) return null;
        return new NodeSchema
        {
            Namespace = ROOT.Equals(nodeEntity.Namespace, StringComparison.OrdinalIgnoreCase) ? null : nodeEntity.Namespace,
            Name = nodeEntity.Name,
            Kind = nodeEntity.Kind,
            Extensions = nodeEntity.Extensions?.DeepClone() as JsonObject
        };
    }
    
    #endregion
}
