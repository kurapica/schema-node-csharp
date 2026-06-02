using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using Type = System.Type;

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
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, Type[]?, string?> typeResolver)
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
        Dictionary<StructFieldSchema, PropertyInfo> fields = [];
        
        // Check generic types
        Type[] genericArgs = type.GetGenericArguments();
        
        foreach (PropertyInfo p in type
             .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
             .Where(p =>
                 p.GetMethod?.IsPrivate != true &&
                 p.GetCustomAttribute<SchemaIgnoreAttribute>() == null &&
                 p is { CanRead: true, CanWrite: true })
             .OrderBy(p => p.MetadataToken))
        {
            string fieldName = p.Name.ToCamelCase();

            // Explicit [Meta<ValueType>] on the property overrides type resolution
            StructFieldSchema field = new()
            {
                Name = fieldName,
                Type = p.GetMetaProperty<SchemaType>()?.GetValue<string>() ?? runtime.GetTypeSchema(p.PropertyType) ?? "",
            };
            field.SetProperty<Display, LocaleString>(type.GetSummaryFromXmlDoc(p) ?? $"{schema.FullName}.{fieldName}");
            
            // to avoid the cycle ref, resolve the field type later
            fields.Add(field, p);
            
            // Extension Properties
            foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_STRUCT_FIELD))
                field.SetProperty(property);

            // Direct [Relation<T>] attributes declared on the field itself are aggregated to struct relations.
            // Do not inspect Property-type relations here; those are dynamically assembled later.
            foreach (IRelationAttribute relation in p.GetCustomAttributes(inherit: false).OfType<IRelationAttribute>())
                relations.Add(relation.GetRelationSchema(runtime, fieldName, typeResolver));

            // [Meta<PrimaryIndex>] → array primary keys
            foreach (PrimaryIndex idx in p.GetMetaProperties<PrimaryIndex>())
                AddOrderedField(primaries, idx.Order, fieldName);

            // [Meta<UniqueIndex>] → unique indexes
            foreach (UniqueIndex idx in p.GetMetaProperties<UniqueIndex>())
                AddIndex(indexes, idx.Value, fieldName, idx.Order, isUnique: true);
            
            // [Meta<Index>] -> indexes
            foreach (Property.Core.Index idx in p.GetMetaProperties<Property.Core.Index>())
                AddIndex(indexes, idx.Value, fieldName, idx.Order, isUnique: false);

            fieldConfigs.Add(field);
        }

        string[]? primaryFields = BuildFields(primaries);
        DataIndex[]? dataIndexes = BuildIndexes(indexes.Values);

        StructSchema structSchema = new() { Fields = fieldConfigs.ToArray() };
        if (relations.Count > 0)
            structSchema.SetProperty<Relations, RelationSchema[]>(relations.ToArray());
        
        // Generics
        if (genericArgs.Length > 0)
            structSchema.SetProperty<Generics, GenericParameter[]>(genericArgs
                .Select(g => g.GetTypeDetail())
                .Select(g=>
                new GenericParameter (
                    typeResolver(g.CoreType, @namespace, genericArgs)!,
                    g is { AnyArray: false, Number: true } ? [NS_SYSTEM_NUMBER] : null
                )).ToArray());
        
        schema.SetProperty<StructProperty, StructSchema>(structSchema);
        
        // save the struct schema
        yield return schema;

        // Generate the array schema
        if (primaries is { Count: > 0 } || dataIndexes is { Length: > 0 })
        {
            // Also generate a companion array schema when primary keys, indexes, or nested types are present
            NodeSchema array = NodeSchema.Create(SCHEMA_KIND_ARRAY, @namespace, $"{name}s", null, 
                $"{Locale.LIST_PREFIX}{{@{schema.FullName}}}{Locale.LIST_SUFFIX}");
            ArraySchema arraySchema = new() { Element = schema.FullName };
            if (primaryFields is  { Length: > 0 })
                arraySchema.SetProperty<Primary, string[]>(primaryFields);
            if (dataIndexes is { Length: > 0 })
                arraySchema.SetProperty<Indexes, DataIndex[]>(dataIndexes);
            array.SetProperty<ArrayProperty, ArraySchema>(arraySchema);

            yield return array;
        }

        bool changed = false;
        foreach ((StructFieldSchema field, PropertyInfo p) in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Type))
            {
                field.Type = typeResolver(p.PropertyType, @namespace, genericArgs) ??
                             throw new Exception(
                                 $"Failed to resolve type for field {field.Name} of struct {schema.FullName}");
                changed = true;
            }

            NodeSchema? fieldTypeSchema = !string.IsNullOrWhiteSpace(field.Type) ? runtime.GetSystemSchema(field.Type) : null;
            if (fieldTypeSchema == null) throw new Exception($"Failed to resolve type for field {field.Name} of struct {schema.FullName}");

            var detail = p.PropertyType.GetTypeDetail();
            if (detail.AnyArray && !fieldTypeSchema.Kind.Equals(SCHEMA_KIND_ARRAY))
                field.Type = runtime.GetSystemArraySchema(field.Type) ?? throw new Exception($"Failed to resolve array schema for field {field.Name} of struct {schema.FullName}");
            
            // Extension Properties
            foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(fieldTypeSchema.Kind))
            {
                field.SetProperty(property);
                changed = true;
            }

            if (fieldTypeSchema.Kind.Equals(SCHEMA_KIND_ARRAY))
            {
                ArraySchema arraySchema = fieldTypeSchema.GetProperty<ArrayProperty>()?.Value
                    ?? throw new Exception($"Failed to get array schema for field {field.Name} of struct {schema.FullName}");
                NodeSchema element = runtime.GetSystemSchema(arraySchema.Element) 
                    ?? throw new Exception($"Failed to get array schema for field {field.Name} of struct {schema.FullName}");
                foreach (IProperty property in p.GetMetaPropertiesForSchema<IProperty>(element.Kind))
                {
                    field.SetProperty(property);
                    changed = true;
                }
            }
        }
        if (!changed) yield break;

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

            dataIndexes.Add(new DataIndex(
                Name : index.Name,
                Fields : fields,
                IsUnique : index.IsUnique
            ));
        }

        return dataIndexes.Count > 0 ? dataIndexes.ToArray() : null;
    }

    private sealed class PendingIndex(string name, bool isUnique)
    {
        public string Name { get; } = name;

        public bool IsUnique { get; set; } = isUnique;

        public List<(int Order, string FieldName)> Fields { get; } = [];
    }
}
