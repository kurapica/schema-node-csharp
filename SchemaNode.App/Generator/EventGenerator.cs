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
using SchemaNode.Property.App;

namespace SchemaNode.Generator;

public class EventGenerator: INodeSchemaGenerator
{
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?>? typeResolver = null)
    {
        if (!type.IsAssignableTo(typeof(BaseEvent))) yield break;
        Type[] genericArgs = type.GetGenericArguments();
        TypeDetail[] genInfos = genericArgs.Select(g => g.GetTypeDetail()).ToArray();
        GenericParameter[]? genericDeclare = type.GetMetaProperty<Generics>()?.Value;

        NodeSchema schema = NodeSchema.Create(runtime, NODE_KIND_EVENT, @namespace, name, type);
        if (typeResolver == null)
        {
            yield return schema;
            yield break;
        }
        
        // Event schema
        EventSchema eventSchema = new EventSchema();
        foreach (IProperty prop in type.GetMetaPropertiesForSchema<IProperty>(runtime, SCHEMA_KIND_NODE_EVENT))
            eventSchema.SetProperty(prop);

        // Generics
        if (genericArgs.Length > 0 && genericDeclare is null)
        {
            genericDeclare = genInfos.Select(g => new GenericParameter(
                    typeResolver(g.CoreType, @namespace, genericArgs)!,
                    g.Number ? (g.OnlyFloat ? [NS_SYSTEM_NUMBER] : [NS_SYSTEM_NUMBER, NS_SYSTEM_INT]) : null
                )
            ).ToArray();
            eventSchema.SetProperty<Generics, GenericParameter[]>(genericDeclare);
        }

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
                List<RelationSchema> relations = [];
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
                    arg.SetProperty<Display, LocaleString>(ctor.GetSummaryFromXmlDoc(p) ??  arg.Name);
                    
                    // Default
                    if (defaultProp?.Value != null)
                        arg.SetProperty<Default, object>(defaultProp.Value);
                    
                    // Extension Properties
                    foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(runtime, SCHEMA_KIND_NODE_FUNC_ARG))
                        arg.SetProperty(property);

                    // Params
                    if (p.IsDefined(typeof(ParamArrayAttribute), false))
                        throw new Exception($"Param array attribute {p.Name} is not allowed");

                    // Check dynamic type
                    arg.Type = p.GetMetaProperty<SchemaType>()?.GetValue<string>() 
                               ?? typeResolver(pt.Type, @namespace, null)
                               ?? throw new Exception($"Can't resolve parameter type for constructor in {@type.FullName}");

                    if (!(genericDeclare is { Length: > 0 } &&
                           genericDeclare.Any(g => g.Name.Equals(arg.Type, StringComparison.OrdinalIgnoreCase))))
                    {
                        NodeSchema? fieldTypeSchema =
                            !string.IsNullOrWhiteSpace(arg.Type) ? runtime.GetSystemSchema(arg.Type) : null;
                        if (fieldTypeSchema == null)
                            throw new Exception($"Failed to resolve type for argument {arg.Name} of event {schema.FullName}");

                        if (pt.AnyArray && !fieldTypeSchema.Kind.Equals(NODE_KIND_ARRAY))
                            arg.Type = runtime.GetSystemArraySchema(arg.Type) ??
                                         throw new Exception($"Failed to resolve array schema for argument {arg.Name} of event {schema.FullName}");

                        // Extension Properties
                        foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(runtime, runtime.GetSchemaKindByNodeKind(fieldTypeSchema.Kind)))
                            arg.SetProperty(property);

                        if (fieldTypeSchema.Kind.Equals(NODE_KIND_ARRAY))
                        {
                            ArraySchema arraySchema = fieldTypeSchema.GetProperty<ArrayProperty>()?.Value
                                                      ?? throw new Exception($"Failed to get array schema for argument {arg.Name} of event {schema.FullName}");

                            string eleName = arraySchema.Element;
                            if (arraySchema.GetProperty<Generics>()?.Value is { } generics &&
                                generics.Any(g => g.Name.Equals(eleName, StringComparison.OrdinalIgnoreCase)))
                                eleName = runtime.GetSystemSchemaGenericArguments(fieldTypeSchema.FullName).FirstOrDefault() ??
                                          eleName;

                            NodeSchema? element = runtime.GetSystemSchema(eleName);
                            if (element != null)
                                foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(runtime, runtime.GetSchemaKindByNodeKind(element.Kind)))
                                    arg.SetProperty(property);
                        }
                    }


                    // Argument Level Relations
                    foreach (IRelationAttribute relation in p.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                        relations.Add(relation.GetRelationSchema(arg.Name));

                    eventSchema.Args[i] = arg;
                }

                // Function level relations
                foreach (var relation in ctor.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                    relations.Add((relation.GetRelationSchema(NODE_SELF)));

                foreach (var relation in type.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                    relations.Add((relation.GetRelationSchema(NODE_SELF)));

                // Set relations
                if (relations.Count > 0)
                    eventSchema.SetProperty<Relations, RelationSchema[]>(relations.ToArray());
            }
        }
        
        // payload type
        Type? payloadType = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventPayload<>))?.GetGenericArguments()[0];
        if (payloadType != null)
        eventSchema.Payload = type.GetMetaProperty<PayloadType>()?.Value ?? (payloadType != null 
                ? (typeResolver(payloadType, @namespace, genericArgs) ?? throw new  Exception($"Can't resolve payload type for {payloadType.FullName}")) 
                : null);
        
        schema.SetProperty<EventProperty, EventSchema>(eventSchema);
        yield return schema;
    }
}