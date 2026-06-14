using SchemaNode.Event;
using SchemaNode.Property;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Generator;

public class EventGenerator: INodeSchemaGenerator
{
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?> typeResolver)
    {
        if (!type.IsAssignableTo(typeof(Event.Event))) yield break;
        
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_EVENT, @namespace, name, type);
        EventSchema eventSchema = new EventSchema();
        
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?.GetGenericArguments()[0];
        if (payloadType != null)
        {
            Type[] generics = type.GetGenericArguments();

            if (generics.Length > 0)
                eventSchema.SetProperty<Generics, GenericParameter[]>(
                    generics.Select(g => g.GetTypeDetail()).Select(g => 
                        new GenericParameter (
                            typeResolver(g.CoreType, @namespace, generics)!,
                            g.Number ? [g.OnlyFloat ? NS_SYSTEM_DOUBLE : NS_SYSTEM_NUMBER] : null
                        )
                    ).ToArray());
        
            eventSchema.Payload = typeResolver(payloadType, @namespace, generics);
        }

        schema.SetProperty<EventProperty, EventSchema>(eventSchema);
        yield return schema;
    }
}