using SchemaNode.Attribute;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Record;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using NodeType = SchemaNode.Property.Schema.NodeType;
using SchemaType = SchemaNode.Property.Schema.SchemaType;

namespace SchemaNode.Schema;

/// <summary>
/// The event schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeType>(typeof(EventType))]
[Meta<SchemaGenerator>(typeof(EventGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.event.schema")]
public sealed class EventSchema: ExtensibleSchema
{
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
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_EVENT)]
public sealed class EventProperty: Property<EventSchema>;

/// <summary>
/// Represents the event type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.event.type")]
public class EventType: AnyType;