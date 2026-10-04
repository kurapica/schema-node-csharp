using SchemaNode.Attribute;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using SchemaNode.Property.Struct;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using RuntimeEventType = SchemaNode.Runtime.EventType;

namespace SchemaNode.Schema;

/// <summary>
/// The event schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_NODE_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeKind>(NODE_KIND_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeType>(typeof(RuntimeEventType))]
[Meta<SchemaGenerator>(typeof(EventGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_EVENT}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE_EVENT)]
public sealed class EventSchema: PropertyOwner
{
    /// <summary>
    /// The event construct arguments
    /// </summary>
    public FuncArg[]? Args { get; set; } = [];
    
    /// <summary>
    /// The event value type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string? Payload { get; set; }
}

/// <summary>
/// Declare event property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_CORE}.event")]
[Meta<ReadOnly>(true)] // Only system event schema allowed
[Relation<Visible, Relation.Call>("event", NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", NODE_KIND_EVENT)]
public sealed class EventProperty: Property<EventSchema>;

/// <summary>
/// Represents the event type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_EVENT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, NODE_SELF, false, NODE_KIND_EVENT)]
public class EventType: AnyType;