using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Generator;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.Record;
using SchemaNode.Property.Struct;
using SchemaNode.Relation;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;
using RuntimeEventType = SchemaNode.Runtime.EventType;
using SchemaType = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.Schema;

/// <summary>
/// The event schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_NODE_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeKind>(NODE_KIND_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeType>(typeof(RuntimeEventType))]
[Meta<SchemaGenerator>(typeof(EventGenerator))]
[Meta<Append>(typeof(Generics), typeof(Relations))]
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
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_EVENT}.event")]
[Meta<ReadOnly>(true)] // Only system event schema allowed
[Relation<Visible, Relation.Call>("event", NS_SYSTEM_LOGIC_EQ, $"@{nameof(NodeSchema.Kind)}", NODE_KIND_EVENT)]
public sealed class EventProperty: Property<EventSchema>;

/// <summary>
/// Represents the event type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_EVENT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, NODE_SELF, false, NODE_KIND_EVENT)]
public class EventType: AnyType;


/// <summary>
/// The event declare
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_EVENT}.declare")]
public class EventDeclare
{
    /// <summary>
    /// The expected event payload
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// The function name
    /// </summary>
    [Meta<SchemaType>(typeof(EventType))]
    [Relation<Valid, Assign>(nameof(Event), $"{NS_SYSTEM_SCHEMA_REFLECT_EVENT}.{nameof(SystemReflectEvent.withpayload)}", $"@{nameof(Payload)}")]
    public string Event { get; set; } = string.Empty;

    /// <summary>
    /// The event arguments
    /// </summary>
    [Relation<Visible, Call>(nameof(Args), $"{NS_SYSTEM_SCHEMA_REFLECT_EVENT}.{nameof(SystemReflectEvent.hasargs)}", $"@{nameof(Event)}")]
    public CallArg[] Args { get; set; } = [];
}