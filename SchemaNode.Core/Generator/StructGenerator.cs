using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Presentation;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Service;

/// <summary>
/// Generates StructSchema (and optional ArraySchema) from C# class/struct types
/// annotated with [Meta&lt;SchemaType&gt;]. Fields are described via standard data-annotation
/// attributes and [Meta&lt;T&gt;] Meta property declarations, following the same pattern
/// as EnumGenerator and PropertyGenerator.
/// </summary>
internal sealed class StructGenerator : INodeSchemaGenerator
{
    /// <inheritdoc />
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, string?> typeResolver)
    {
        // Only process non-abstract, non-enum classes and value types
        if (type.IsEnum ||
            type is { IsClass: false, IsValueType: false } ||
            type is { IsClass: true, IsAbstract: true } ||
            (type.IsValueType && type.IsPrimitiveLike())) yield break;

        // Build struct node schema
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_STRUCT, @namespace, name, type, type.GetSummaryFromXmlDoc());

        // Generate the struct fields
        List<(int Order, string FieldName)> primaries = [];
        Dictionary<string, PendingIndex> indexes = new(StringComparer.OrdinalIgnoreCase);
        List<RelationSchema> relations = [];
        List<StructFieldSchema> fieldConfigs = [];
        bool solveLater = false;
        Dictionary<string, Type> unSolvedField = new(StringComparer.OrdinalIgnoreCase);

        foreach (PropertyInfo p in type
                     .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     .Where(p =>
                         p.GetMethod?.IsPrivate != true &&
                         p.GetCustomAttribute<SchemaIgnoreAttribute>() == null &&
                         p is { CanRead: true, CanWrite: true })
                     .OrderBy(p => p.MetadataToken))
        {
            string fieldName = p.Name.ToCamelCase();

            // Explicit [Meta<SchemaType>] on the property overrides type resolution
            string? fieldType = p.GetMetaProperty<SchemaType>()?.Value ?? runtime.GetTypeSchema(p.PropertyType);

            StructFieldSchema field = new()
            {
                Name = fieldName,
                Type = fieldType ?? "",
            };
            field.SetProperty<Display, LocaleString>(type.GetSummaryFromXmlDoc(p) ?? $"{schema.FullName}.{fieldName}");
            // to avoid the cycle ref, resolve the field type later
            if (string.IsNullOrWhiteSpace(fieldType))
            {
                solveLater = true;
                unSolvedField.Add(fieldName, p.PropertyType);
            }
            
            // Extension Properties
            foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_STRUCT_FIELD))
                field.SetProperty(property);

            // Direct [Relation<T>] attributes declared on the field itself are aggregated to struct relations.
            // Do not inspect Property-type relations here; those are dynamically assembled later.
            foreach (IRelationAttribute relation in p.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                relations.Add(BuildRelation(runtime, fieldName, relation));

            // [Meta<PrimaryIndex>] → array primary keys
            foreach (PrimaryIndex idx in p.GetMetaProperties<PrimaryIndex>())
                AddOrderedField(primaries, idx.Order, fieldName);

            // [Meta<UniqueIndex>] → unique indexes
            foreach (UniqueIndex idx in p.GetMetaProperties<UniqueIndex>())
                AddIndex(indexes, idx.Value, fieldName, idx.Order, isUnique: true);
            
            // [Meta<Index>] -> indexes
            foreach (SchemaNode.Property.Schema.Index idx in p.GetMetaProperties<SchemaNode.Property.Schema.Index>())
                AddIndex(indexes, idx.Value, fieldName, idx.Order, isUnique: false);

            fieldConfigs.Add(field);
        }

        string[]? primaryFields = BuildFields(primaries);
        DataIndex[]? dataIndexes = BuildIndexes(indexes.Values);

        StructSchema structSchema = new() { Fields = fieldConfigs.ToArray() };
        if (relations.Count > 0)
            structSchema.SetProperty<RelationsProperty, RelationSchema[]>(relations.ToArray());
        schema.SetProperty<StructProperty, StructSchema>(structSchema);
        
        // save the struct schema
        yield return schema;

        // Generate the array schema
        if (primaries is { Count: > 0 } || dataIndexes is { Length: > 0 })
        {
            // Also generate a companion array schema when primary keys, indexes, or nested types are present
            NodeSchema arraySchema = NodeSchema.Create(SCHEMA_KIND_ARRAY, @namespace, $"{name}s", null, 
                $"{Locale.LIST_PREFIX}{{@{schema.FullName}}}{Locale.LIST_SUFFIX}");
            arraySchema.SetProperty<ArrayProperty, ArraySchema>(new ArraySchema
            {
                Element = schema.FullName,
                Primary = primaryFields,
                Indexes = dataIndexes
            });

            yield return arraySchema;
        }

        // Re-generate the struct schema
        if (!solveLater) yield break;
        
        foreach (StructFieldSchema field in structSchema.Fields)
        {
            if (unSolvedField.TryGetValue(field.Name, out Type? propType))
                field.Type = typeResolver(propType, @namespace) ?? throw new Exception($"Type {propType} not found for {type.FullName}'s {field.Name} property");
        }
        schema.SetProperty<StructProperty, StructSchema>(structSchema);
        yield return schema;
    }

    private static void AddOrderedField(List<(int Order, string FieldName)> fields, int order, string fieldName)
        => fields.Add((order, fieldName));

    private static void AddIndex(Dictionary<string, PendingIndex> indexes, string? indexName, string fieldName, int order,
        bool isUnique)
    {
        string resolvedName = string.IsNullOrWhiteSpace(indexName) ? fieldName : indexName;
        if (!indexes.TryGetValue(resolvedName, out PendingIndex? index))
        {
            index = new PendingIndex(resolvedName, isUnique);
            indexes[resolvedName] = index;
        }
        else if (isUnique)
        {
            index.IsUnique = true;
        }

        AddOrderedField(index.Fields, order, fieldName);
    }

    private static string[]? BuildFields(IEnumerable<(int Order, string FieldName)> fields)
    {
        List<string> orderedFields = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach ((_, string fieldName) in fields.OrderBy(f => f.Order))
        {
            if (string.IsNullOrWhiteSpace(fieldName) || !seen.Add(fieldName)) continue;
            orderedFields.Add(fieldName);
        }

        return orderedFields.Count > 0 ? orderedFields.ToArray() : null;
    }

    private static DataIndex[]? BuildIndexes(IEnumerable<PendingIndex> indexes)
    {
        List<DataIndex> dataIndexes = [];

        foreach (PendingIndex index in indexes)
        {
            string[]? fields = BuildFields(index.Fields);
            if (fields == null || fields.Length == 0) continue;

            dataIndexes.Add(new DataIndex
            {
                Name = index.Name,
                Fields = fields,
                IsUnique = index.IsUnique
            });
        }

        return dataIndexes.Count > 0 ? dataIndexes.ToArray() : null;
    }

    private static RelationSchema BuildRelation(ISchemaRuntime runtime, string fieldName, IRelationAttribute relation)
    {
        RelationSchema relationSchema = new()
        {
            Target = fieldName,
            Property = relation.Property.GetPropertyName(),
            Kind = relation.Kind
        };

        IRelationProcess process = relation.GetRelationProcess();
        Type propType = runtime.GetSchemaKindProperty(SCHEMA_KIND_RELATION, process.GetType())
            ?? throw new Exception($"Failed to find relation property for process type '{process.GetType().FullName}'.");
        relationSchema.SetProperty(propType, process);
        return relationSchema;
    }

    private sealed class PendingIndex(string name, bool isUnique)
    {
        public string Name { get; } = name;

        public bool IsUnique { get; set; } = isUnique;

        public List<(int Order, string FieldName)> Fields { get; } = [];
    }
}
