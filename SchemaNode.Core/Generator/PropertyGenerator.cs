using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Service;

/// <summary>
/// Generates PropertySchema from SchemaProperty&lt;T&gt; subclasses annotated with [SchemaPropertyAttribute]
/// </summary>
internal class PropertyGenerator : INodeSchemaGenerator
{
    /// <inheritdoc />
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?>? typeResolver = null)
    {
        Type? valueType = type.GetGenericBaseType(typeof(Property<>))?.GetGenericArguments().ElementAtOrDefault(0);
        if (valueType == null) yield break;
        
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_PROPERTY, @namespace, name, type);
        if (typeResolver == null)
        {
            yield return schema; // take place no details
            yield break;
        }
        
        PropertySchema propSchema = new PropertySchema
        {
            // Name
            Property = type.GetPropertyName(),

            // Type
            Type = type.GetMetaProperty<PropertyValueType>()?.Value ?? typeResolver(valueType, @namespace, null) ??
                throw new Exception($"Type '{valueType}' can't be resolved as schema type."),

            // ForSchemas
            ForSchemas = type.GetMetaProperty<ForSchema>()?.GetValue<string[]>() ?? [],
        };
                
        // Relations
        List<RelationSchema> relations = [];
        
        // Default error
        if (type.IsAssignableTo(typeof(IConstraintProperty)))
            schema.SetProperty<Error, LocaleString>($"{schema.FullName}.error");
        
        // Direct [Relation<T>] attributes declared on the field itself are aggregated to struct relations.
        // Do not inspect Property-type relations here; those are dynamically assembled later.
        foreach (IRelationAttribute relation in type.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
            relations.Add(relation.GetRelationSchema(propSchema.Property));
        if (relations.Count > 0)
            propSchema.SetProperty<Relations, RelationSchema[]>(relations.ToArray());
        
        // Build property schema
        schema.SetProperty<PropertyProperty, PropertySchema>(propSchema);
        
        yield return schema;
    }
}