using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using SchemaType = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.Schema;

/// <summary>
/// The permission policy schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_POLICY, SCHEMA_KIND_ORDER_POLICY)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_POLICY, SCHEMA_KIND_ORDER_POLICY)]
[Meta<NodeType>(typeof(PolicyType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.policy.schema")]
public sealed class PolicySchema: ExtensibleSchema
{
    /// <summary>
    /// The policy items
    /// </summary>
    public PolicyItem[] Items { get; set; } = [];
}

/// <summary>
/// Declare event property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE, SCHEMA_KIND_APP, SCHEMA_KIND_APP_FIELD)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_POLICY)]
public sealed class PolicyProperty: Property<PolicySchema>;

/// <summary>
/// Represents the event type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.event.type")]
public class PolicyType: AnyType;

/// <summary>
/// Represents the union validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_FUNC}.evaluator")]
public class EvaluatorType : FuncType;

/// <summary>
/// The policy item schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.policy.item")]
public sealed class PolicyItem
{
    /// <summary>
    /// The policy scope
    /// </summary>
    public required PolicyScope Scope { get; set; }

    /// <summary>
    /// The policy evaluator
    /// </summary>
    [Meta<SchemaType>(typeof(EvaluatorType))]
    public required string Evaluator { get; set; }

    /// <summary>
    /// The policy combine method
    /// </summary>
    public required PolicyCombine Combine { get; set; }

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? Function { get; set; }
    
    /// <summary>
    /// The status
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }
}
