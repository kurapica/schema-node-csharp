using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Service;

/// <summary>
/// Generates PropertySchema from SchemaProperty&lt;T&gt; subclasses annotated with [SchemaPropertyAttribute]
/// </summary>
internal class PropertyGenerator : INodeSchemaGenerator
{
    /// <inheritdoc />
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, string?> typeResolver)
    {
        Type? valueType = type.GetGenericBaseType(typeof(Property<>))?.GetGenericArguments().ElementAtOrDefault(0);
        if (valueType == null) yield break;
        
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_PROPERTY, @namespace, name, type);
        
        // Build property schema
        schema.SetProperty<PropProperty, PropertySchema>( new PropertySchema()
        {
            // name
            Property = type.GetPropertyName(),
            
            // Type
            Type = typeResolver(valueType, @namespace) ?? throw new Exception($"Type '{valueType}' can't be resolved as schema type."),
            
            // Depends
            Depends = type.GetMetaProperty<Depends>()?.Value?.Select(t => typeResolver(t, @namespace))
                .Where(s => s != null).Cast<string>().ToArray(),
        
            // OptionDepends
            OptionDepends = type.GetMetaProperty<OptionDepends>()?.Value?.Select(t => typeResolver(t, @namespace))
                .Where(s => s != null).Cast<string>().ToArray(),
        
            // ForSchemas
            ForSchemas = type.GetMetaProperty<ForSchema>()?.GetValue<string[]>() 
                         ?? throw new ArgumentException($"Type '{type}' is not a valid as property type."),
        });
        
        yield return schema;
    }
}