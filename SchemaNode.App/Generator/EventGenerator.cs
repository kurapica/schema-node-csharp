using SchemaNode.Event;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Generator;

public class EventGenerator: INodeSchemaGenerator
{
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?> typeResolver)
    {
        if (!type.IsAssignableTo(typeof(Event.Event))) yield break;
        
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?.GetGenericArguments()[0];

        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_EVENT, @namespace, name, type);
        EventSchema eventSchema = new EventSchema();
        if (payloadType != null)
            eventSchema.Payload = typeResolver(payloadType, @namespace, null);
        else if (type.IsAssignableTo(typeof(IEventPayload)))
        {
            eventSchema.SetProperty<Generics, GenericParameter[]>([ new GenericParameter(NS_GENERIC_TYPE)]);
            eventSchema.Payload = NS_GENERIC_TYPE;
        }
        
        schema.SetProperty<EventProperty, EventSchema>(eventSchema);
        yield return schema;
    }
}