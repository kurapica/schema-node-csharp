using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using NodeType = SchemaNode.Property.Schema.NodeType;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using SchemaType = SchemaNode.Property.Schema.SchemaType;

namespace SchemaNode.Schema;

/// <summary>
/// The workflow schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_WORKFLOW, SCHEMA_KIND_ORDER_WORKFLOW)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_WORKFLOW, SCHEMA_KIND_ORDER_WORKFLOW)]
[Meta<NodeType>(typeof(PolicyType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.workflow.schema")]
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
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.workflow.type")]
public class WorkflowType: AnyType;