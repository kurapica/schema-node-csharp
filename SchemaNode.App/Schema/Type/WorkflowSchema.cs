using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Common;
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
[Meta<NodeType>(typeof(PolicyType))]
[Meta<SchemaGenerator>(typeof(WorkflowGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.schema")]
public sealed class WorkflowSchema: ExtensibleSchema
{
    /// <summary>
    /// The workflow mode
    /// </summary>
    public WorkflowMode Mode { get; set;  }
    
    /// <summary>
    /// The workflow return type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? Payload { get; set; }
    
    /// <summary>
    /// The state schema type for creation
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? State { get; set; }
    
    /// <summary>
    /// The session schema type for processing
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? Session { get; set; }
    
    /// <summary>
    /// The workflow arguments fetch from workflow context
    /// </summary>
    public FuncArg[]? Args { get; set; } = [];
}

/// <summary>
/// Declare event property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_WORKFLOW)]
public sealed class WorkflowProperty: Property<WorkflowSchema>;

/// <summary>
/// Represents the event type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_WORKFLOW}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_WORKFLOW)]
public class WorkflowType: AnyType;