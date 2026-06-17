using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using SchemaType = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.Schema;

/// <summary>
/// The workflow schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_WORKFLOW, SCHEMA_KIND_ORDER_WORKFLOW)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_WORKFLOW, SCHEMA_KIND_ORDER_WORKFLOW)]
[Meta<NodeType>(typeof(WorkflowType))]
[Meta<SchemaGenerator>(typeof(WorkflowGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.schema")]
[Meta<Attach>(SCHEMA_KIND_WORKFLOW)]
[Meta<Append>(typeof(Generics))]
public sealed class WorkflowSchema: ExtensibleSchema
{
    /// <summary>
    /// The workflow kind
    /// </summary>
    [Meta<SchemaType>(typeof(WorkflowKind))]
    public string Kind { get; set; } = null!;
    
    /// <summary>
    /// The workflow result type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? Payload { get; set; }
    
    /// <summary>
    /// The state schema type for creation
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? Settings { get; set; }
    
    /// <summary>
    /// The session schema type for processing
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? Session { get; set; }
    
    /// <summary>
    /// The workflow arguments
    /// </summary>
    public FuncArg[]? Args { get; set; } = [];
}

/// <summary>
/// Declare event property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.workflow")]
[Meta<ReadOnly>(true)] // only system workflow schema allowed
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_WORKFLOW)]
public sealed class WorkflowProperty: Property<WorkflowSchema>;

/// <summary>
/// Represents the event type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_WORKFLOW)]
public class WorkflowType: AnyType;