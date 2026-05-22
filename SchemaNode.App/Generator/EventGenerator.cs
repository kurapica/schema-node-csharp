using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.App.Generator;

/// <summary>
/// Generates <see cref="EventSchema"/> node schemas from C# types annotated with
/// <c>[Meta&lt;SchemaType&gt;(name)]</c> within the event schema kind.
/// Mirrors the <c>EnumGenerator</c> pattern in SchemaNode.Core.
/// Register this generator via <c>[Meta&lt;SchemaGenerator&gt;(typeof(EventGenerator))]</c> on EventSchema.
/// </summary>
public sealed class EventGenerator : INodeSchemaGenerator
{
    /// <inheritdoc />
    public IEnumerable<NodeSchema> GenerateSchema(
        SchemaRuntime runtime,
        Type type,
        string @namespace,
        string name,
        Func<Type, string, string?> typeResolver)
    {
        // Resolve payload type from a "Payload" property on the type
        string? payloadSchemaType = null;
        PropertyInfo? payloadProp = type.GetProperty("Payload");
        if (payloadProp != null)
        {
            // Prefer explicit [Meta<SchemaType>] annotation on the property
            SchemaType? schemaTypeAttr = payloadProp.GetMetaProperty<SchemaType>();
            if (schemaTypeAttr?.HasValue == true)
                payloadSchemaType = schemaTypeAttr.Value;
            else if (payloadProp.PropertyType != typeof(string))
                payloadSchemaType = typeResolver(payloadProp.PropertyType, @namespace);
        }

        // Use Core's NodeSchema.Create to handle namespace splitting, display and property scanning
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_EVENT, name, type);

        // Attach the EventSchema property with the resolved payload type
        schema.SetProperty<EventProperty, EventSchema>(new EventSchema
        {
            Payload = payloadSchemaType,
        });

        yield return schema;
    }
}
