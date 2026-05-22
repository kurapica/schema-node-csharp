using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Property.Schema.NodeType;
using ForSchema = SchemaNode.Property.Schema.ForSchema;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The permission policy schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_POLICY, SCHEMA_KIND_ORDER_POLICY)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_POLICY, SCHEMA_KIND_ORDER_POLICY)]
[Meta<NodeType>(typeof(PolicyType))]
[Meta<SchemaType>($"{NS_APP_SCHEMA_POLICY}.schema")]
public sealed class PolicySchema : ExtensibleSchema
{
    /// <summary>
    /// The policy items
    /// </summary>
    public PolicyItem[] Items { get; set; } = [];

    /// <inheritdoc />
    public override void CombineExtensions(ExtensibleSchema? other, ISchemaRuntime? runtime = null)
    {
        if (other is not PolicySchema otherPolicy) return;
        base.CombineExtensions(otherPolicy, runtime);
        if (otherPolicy.Items is { Length: > 0 } && (Items == null || Items.Length == 0))
            Items = otherPolicy.Items[..];
    }
}

/// <summary>
/// Declare policy property for node schema — enables GetPropertyValue&lt;PolicySchema&gt;() on PolicyType
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
public sealed class PolicyProperty : Property<PolicySchema>;

/// <summary>
/// The policy item — one evaluation rule within a PolicySchema
/// </summary>
[Meta<SchemaType>($"{NS_APP_SCHEMA_POLICY}.item")]
public sealed class PolicyItem : ExtensibleSchema
{
    /// <summary>
    /// The policy scope
    /// </summary>
    public required PolicyScope Scope { get; set; }

    /// <summary>
    /// The policy evaluator function name
    /// </summary>
    [Meta<SchemaType>($"{NS_APP_SCHEMA_FUNC}.schema")]
    public required string Evaluator { get; set; }

    /// <summary>
    /// The policy combine method
    /// </summary>
    public required PolicyCombine Combine { get; set; }

    /// <summary>
    /// The resolved function type (set at runtime, not serialized)
    /// </summary>
    [JsonIgnore]
    public Runtime.FunctionType? Function { get; set; }

    /// <summary>
    /// Runtime load status
    /// </summary>
    [JsonIgnore]
    public string? Status { get; set; }
}
