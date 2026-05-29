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

        PropertySchema propSchema = new PropertySchema
        {
            // Name
            Property = type.GetPropertyName(),

            // Type
            Type = type.GetMetaProperty<PropertyValueType>()?.Value ?? typeResolver(valueType, @namespace, null) ??
                throw new Exception($"Type '{valueType}' can't be resolved as schema type."),

            // Depends
            Depends = type.GetMetaProperty<Depend>()?.Value?.Select(t => t.GetPropertyName()).ToArray(),

            // Override
            Overrides = type.GetMetaProperty<Override>()?.Value?.Select(t => t.GetPropertyName()).ToArray(),

            // ForSchemas
            ForSchemas = type.GetMetaProperty<ForSchema>()?.GetValue<string[]>() ??
                         throw new ArgumentException($"Type '{type}' is not a valid as property type."),

            // Static
            Static = type.GetMetaProperty<Static>()?.GetValue<bool>(),

            // Stackable
            Stackable = type.GetMetaProperty<Stackable>()?.GetValue<bool>(),
        };
        
        // Relations
        List<RelationSchema> relations = [];
        // Direct [Relation<T>] attributes declared on the field itself are aggregated to struct relations.
        // Do not inspect Property-type relations here; those are dynamically assembled later.
        foreach (IRelationAttribute relation in type.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
            relations.Add(BuildRelation(runtime, propSchema.Property, relation));
        if (relations.Count > 0)
            propSchema.SetProperty<Relations, RelationSchema[]>(relations.ToArray());
        
        // Build property schema
        schema.SetProperty<PropProperty, PropertySchema>(propSchema);
        
        yield return schema;
    }
    
    
    private static RelationSchema BuildRelation(ISchemaRuntime runtime, string fieldName, IRelationAttribute relation)
    {
        RelationSchema relationSchema = new()
        {
            Target = relation.Target ?? fieldName,
            Property = relation.Property.GetPropertyName(),
            Kind = relation.Kind
        };

        IRelationProcessBuilder process = relation.GetRelationProcess();
        Type propType = runtime.GetSchemaKindProperty(SCHEMA_KIND_RELATION, process.GetType())
                        ?? throw new Exception($"Failed to find relation property for process type '{process.GetType().FullName}'.");
        relationSchema.SetProperty(propType, process);
        return relationSchema;
    }
}