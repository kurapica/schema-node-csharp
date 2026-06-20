using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Core;
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
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?> typeResolver)
    {
        Type? valueType = type.GetGenericBaseType(typeof(Property<>))?.GetGenericArguments().ElementAtOrDefault(0);
        if (valueType == null) yield break;
        
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_PROPERTY, @namespace, name, type);

        string[]? forSchemas = type.GetMetaProperty<ForSchema>()?.GetValue<string[]>();
        if (forSchemas == null)
            yield break; // ForSchema is optional — properties without it use the Attach mechanism
        
        PropertySchema propSchema = new PropertySchema
        {
            // Name
            Property = type.GetPropertyName(),

            // Type
            Type = type.GetMetaProperty<PropertyValueType>()?.Value ?? typeResolver(valueType, @namespace, null) ??
                throw new Exception($"Type '{valueType}' can't be resolved as schema type."),

            // ForSchemas
            ForSchemas = forSchemas,

            // Static
            Static = type.GetMetaProperty<Static>()?.GetValue<bool>(),

            // Stackable
            Stackable = type.GetMetaProperty<Stackable>()?.GetValue<bool>(),
        };
                
        // Relations — NOT processed during schema generation.
        // Direct [Relation<T>] attributes on property types are dynamically assembled at runtime.
        // Processing them here causes infinite recursion since resolving target types
        // triggers further PropertyGenerator calls.
        
        // Build property schema
        schema.SetProperty<Schema.Property, PropertySchema>(propSchema);
        
        yield return schema;
    }
}