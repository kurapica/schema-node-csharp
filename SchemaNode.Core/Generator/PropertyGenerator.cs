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
        schema.SetProperty<PropProperty, PropertySchema>(new PropertySchema
        {
            // Name
            Property = type.GetPropertyName(),
            
            // Type
            Type = type.GetMetaProperty<PropertyValueType>()?.Value ?? typeResolver(valueType, @namespace) ?? throw new Exception($"Type '{valueType}' can't be resolved as schema type."),
            
            // Depends
            Depends = type.GetMetaProperty<Depend>()?.Value?.Select(t => t.GetPropertyName()).ToArray(),
            
            // Override
            Overrides = type.GetMetaProperty<Override>()?.Value?.Select(t => t.GetPropertyName()).ToArray(),
        
            // ForSchemas
            ForSchemas = type.GetMetaProperty<ForSchema>()?.GetValue<string[]>()  ?? throw new ArgumentException($"Type '{type}' is not a valid as property type."),
            
            // ForValueTypes
            ForTypes = type.GetMetaProperty<ForType>()?.GetValue<string[]>(),
        
            // Static
            Static = type.GetMetaProperty<Static>()?.GetValue<bool>(),
        });
        
        yield return schema;
    }
}