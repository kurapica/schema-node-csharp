using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Event;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Service;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Generator;

public class EventGenerator: INodeSchemaGenerator
{
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?>? typeResolver = null)
    {
        if (!type.IsAssignableTo(typeof(Event.BaseEvent))) yield break;
        
        if (type.GetGenericArguments() is {  Length: > 0 })
            throw new Exception($"BaseEvent type {type.FullName} can't be generic");
        
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_EVENT, @namespace, name, type);
        if (typeResolver == null)
        {
            yield return schema;
            yield break;
        }
        
        // Event schema
        EventSchema eventSchema = new EventSchema();
       
        // Constructor Arguments
        ConstructorInfo[] ctors = type.GetConstructors()
            .Where(c => c.GetCustomAttribute<SchemaIgnoreAttribute>() == null).ToArray();
        if (ctors.Length > 1)
            throw new Exception($"BaseEvent type {type.FullName} has multiple constructors, which is not allowed");

        if (ctors.Length == 1)
        {
            ConstructorInfo ctor = ctors.First();
            ParameterInfo[] parameters = ctor.GetParameters();
            if (parameters.Length > 0)
            {
                TypeDetail[] paramInfos = parameters.Select(p => p.ParameterType.GetTypeDetail()).ToArray();
                eventSchema.Args = new FuncArg[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    ParameterInfo p = parameters[i];
                    TypeDetail pt = paramInfos[i];
                    Default? defaultProp = p.GetMetaProperty<Default>();
            
                    FuncArg arg = new ()
                    {
                        Name = p.Name ?? $"arg{i}",
                    };
                    
                    // Require
                    if (!(pt.Nullable || p.HasDefaultValue ||
                          _nullabilityContext.Create(p).ReadState == NullabilityState.Nullable ||
                          p.GetCustomAttributesData().FirstOrDefault(a =>
                              a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute") != null ||
                          defaultProp != null))
                        arg.SetProperty<Require, bool>(true);
                    
                    // Display
                    arg.SetProperty<Display, LocaleString>(ctor.GetSummaryFromXmlDoc(p) ??  $"{schema.FullName}.{arg.Name}");
                    
                    // Default
                    if (defaultProp?.Value != null)
                        arg.SetProperty<Default, object>(defaultProp.Value);
                    
                    // Extension Properties
                    foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_FUNC_ARG))
                        arg.SetProperty(property);

                    // Params
                    if (p.IsDefined(typeof(ParamArrayAttribute), false))
                        throw new Exception($"Param array attribute {p.Name} is not allowed");

                    // Check dynamic type
                    arg.Type = p.GetMetaProperty<SchemaType>()?.GetValue<string>() 
                               ?? typeResolver(pt.Type, @namespace, null)
                               ?? throw new Exception($"Can't resolve parameter type for constructor in {@type.FullName}");

                    eventSchema.Args[i] = arg;
                }
            }
        }
        
        // Default payload type
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?.GetGenericArguments()[0];
        if (payloadType != null)
            eventSchema.Payload = typeResolver(payloadType, @namespace, null) ?? throw new  Exception($"Can't resolve payload type for {payloadType.FullName}");
        
        schema.SetProperty<EventProperty, EventSchema>(eventSchema);
        yield return schema;
    }
}