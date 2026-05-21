using System.Text.Json.Nodes;
using SchemaNode.Attribute;
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
using SchemaNode.App.Generator;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// Declare event property for node schema — enables GetPropertyValue&lt;EventSchema&gt;() on EventType
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
public sealed class EventProperty : Property<EventSchema>;

/// <summary>
/// The event schema — describes an event that carries a typed payload.
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_EVENT, SCHEMA_KIND_ORDER_EVENT)]
[Meta<NodeType>(typeof(EventType))]
[Meta<SchemaGenerator>(typeof(EventGenerator))]
[Meta<SchemaType>($"{NS_APP_SCHEMA_EVENT}.schema")]
public sealed class EventSchema : ExtensibleSchema
{
    /// <summary>
    /// The payload value type (schema type name)
    /// </summary>
    public string? Payload { get; set; }

    /// <inheritdoc />
    public override void CombineExtensions(ExtensibleSchema? other, ISchemaRuntime? runtime = null)
    {
        if (other is not EventSchema otherEvent) return;
        base.CombineExtensions(otherEvent, runtime);
        Payload ??= otherEvent.Payload;
    }
}
