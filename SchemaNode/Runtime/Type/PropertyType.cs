using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory property schema representation
/// </summary>
public sealed class PropertyType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The property name, such as "uplimit", "lowlimit", "pattern", etc.
    /// </summary>
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The value type, null means use the target node type
    /// </summary>
    public string? ValueType { get; internal set; }

    /// <summary>
    /// The required propSchema names that this propSchema depends on
    /// </summary>
    public string[]? Depends { get; internal set; }

    /// <summary>
    /// The optional propSchema names that this propSchema depends on
    /// </summary>
    public string[]? OptionDepends { get; internal set; }

    /// <summary>
    /// The schema types that this propSchema applies to
    /// </summary>
    public SchemaType[] ForSchemas { get; internal set; } = [];

    /// <summary>
    /// For value kinds
    /// </summary>
    public ValueSchemaType[]? ForValues { get; internal set; }

    /// <summary>
    /// Include the value type array
    /// </summary>
    public bool? IncludeArray { get; internal set; }

    #endregion

    #region Ref

    /// <summary>
    /// The value schema type
    /// </summary>
    public AnySchemaType? ValueSchemaType { get; internal set; }

    #endregion

    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Property;

    /// <inheritdoc />
    public override bool IsUsed => true;

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        PropertySchema? propSchema = schema.Property;

        // Data
        Property = !string.IsNullOrWhiteSpace(propSchema?.Property) ? propSchema.Property : propSchema?.Name ?? string.Empty;
        ValueType = propSchema?.ValueType;
        Depends = propSchema?.Depends;
        OptionDepends = propSchema?.OptionDepends;
        ForSchemas = propSchema?.ForSchemas ?? [];
        ForValues = propSchema?.ForValues;
        IncludeArray = propSchema?.IncludeArray;

        if (string.IsNullOrWhiteSpace(Property) || propSchema == null) Status = SchemaNodeStatus.NoDefinition;

        ValueSchemaType = !string.IsNullOrWhiteSpace(ValueType) ? await context.GetSchemaTypeAsync(ValueType) : null;
        if (!string.IsNullOrWhiteSpace(ValueType) && ValueSchemaType == null) Status = SchemaNodeStatus.PropertyHasWrongValueType;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false)
    {
        return null;
    }

    #endregion

    #region Static Feature

    /// <summary>
    /// Generate the propSchema schema from the given propSchema type
    /// </summary>
    public static NodeSchema[] GenerateSystemProperty(Type type, string? ns = null)
    {
        Type? superClass = type.BaseType;
        while (superClass != null && (!superClass.IsGenericType || superClass.GetGenericTypeDefinition() != typeof(SchemaProperty<>)))
            superClass = superClass.BaseType;
        if (superClass == null) return [];

        // Get the value type T from SchemaProperty<T>
        Type valueType = superClass.GetGenericArguments()[0];

        // Gets the extensions property kinds
        Dictionary<string, JsonElement> extensions = [];
        Type iProp = typeof(IProperty);

        foreach (Type interfaceType in type.GetInterfaces())
        {
            // marker
            if (interfaceType != iProp && interfaceType.IsAssignableTo(iProp) && interfaceType.GetCustomAttribute<SchemaPropertyKindAttribute>() is { } kind && !string.IsNullOrWhiteSpace(kind.Kind))
            {
                // 1 means mutually exclusive, true means normal
                extensions[kind.Kind] = kind.MutuallyExclusive ? JsonSerializer.SerializeToElement(1) : JsonSerializer.SerializeToElement(true);
            }
        }

        // Get the propSchema name
        SchemaPropertyAttribute? attr = type.GetCustomAttribute<SchemaPropertyAttribute>();
        string name= type.Name;
        if (name.EndsWith("Property", StringComparison.OrdinalIgnoreCase))
            name = name[..^"Property".Length];
        if (name.EndsWith("Prop", StringComparison.OrdinalIgnoreCase))
            name = name[..^"Prop".Length];
        name = name.ToCamelCase();

        // Register the property type
        string typeName = $"{NS_SYSTEM_PROPERTY}.{name.ToLower()}";
        NodeSchema propSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Property,
            Display = attr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Property = new PropertySchema
            {
                Property = !string.IsNullOrWhiteSpace(attr?.Name) ? attr.Name : name,
                // system.array means use the target node type's array
                ValueType = valueType == typeof(ArrayTypeNode) ? NS_SYSTEM_ARRAY : valueType.IsAssignableTo(typeof(AnySchemaNode)) ? null : attr?.SchemaType ?? valueType.GetSchemaType(true, ns),
                // case [nameof(PrecisionProperty)] is used in Depends/OptionDepends, normalize to property name (strip "Property"/"Prop" suffix + camelCase)
                Depends = attr?.Depends?.Select(static p => p.EndsWith("Property", StringComparison.OrdinalIgnoreCase) ? p[..^"Property".Length].ToCamelCase() : p.EndsWith("Prop", StringComparison.OrdinalIgnoreCase) ? p[..^"Prop".Length].ToCamelCase() : p).ToArray(),
                OptionDepends = attr?.OptionDepends?.Select(static p => p.EndsWith("Property", StringComparison.OrdinalIgnoreCase) ? p[..^"Property".Length].ToCamelCase() : p.EndsWith("Prop", StringComparison.OrdinalIgnoreCase) ? p[..^"Prop".Length].ToCamelCase() : p).ToArray(),
                ForSchemas = attr?.ForSchemas ?? [],
                ForValues = attr?.ForValues,
                IncludeArray = attr?.IncludeArray,
                Extensions = extensions.Count > 0 ? extensions : null,
            }
        };

        if (SystemLocale.HasLocales)
            SystemLocale.Translate(propSchema.Display, propSchema.Name);

        // record the type
        propertyMap[typeName] = type;
        return [propSchema];
    }

    /// <summary>
    /// Build propSchema instances whose property name appears in <paramref name="extensions"/>,
    /// filtered by <paramref name="schemaType"/>, with values deserialized from the matching
    /// <see cref="JsonElement"/>. The returned list is topologically sorted by
    /// <see cref="SchemaPropertyAttribute.Depends"/> (dependencies first).
    /// Properties referenced by <paramref name="relationProps"/> but absent from <paramref name="extensions"/>
    /// are created with empty values so they can receive override values from relations at runtime.
    /// </summary>
    public static IEnumerable<T> GetProperties<T>(SchemaContext context, SchemaType schemaType, Dictionary<string, JsonElement> extensions, AnySchemaType? valueType = null, IReadOnlyList<string>? relationProps = null, bool fullConstraintList = false) where T: IProperty
    {
        if (SchemaContext.SystemProperty == null) return [];

        // 1. Collect matching properties from extensions
        List<(string name, PropertyType entry, T instance)> matched = [];

        foreach (PropertyType entry in SchemaContext.SystemProperty.SchemaNodes.Values.Cast<PropertyType>())
        {
            if (!propertyMap.TryGetValue(entry.Name, out Type? impl)) continue;
            if (!typeof(T).IsAssignableFrom(impl)) continue;
            if (!entry.ForSchemas.Contains(schemaType)) continue;
            if (valueType != null && entry.ForValues != null && !entry.ForValues.Any(v => MatchSchemaValueType(valueType, v, entry.IncludeArray))) continue;

            // Prop name must exist in extensions (case-insensitive)
            bool hasData = extensions.TryGetValue(entry.Property, out JsonElement element);
            bool allowEmpty = fullConstraintList && (typeof(IConstraintProperty).IsAssignableFrom(impl)) || relationProps != null && relationProps.Contains(entry.Property, StringComparer.OrdinalIgnoreCase);
            if (!hasData && !allowEmpty) continue;

            // Create instance and deserialize value
            if (Activator.CreateInstance(impl) is not T instance) continue;

            // Get T from Constraint<T> and set Value
            instance.Name = entry.Property;
            instance.ForArrayOnly = entry.ForValues != null && entry.ForValues.All(v => v == Enum.ValueSchemaType.Array);
            if (hasData)
            {
                instance.SetValue(context, element, valueType);

                if (instance.HasValue)
                {
                    matched.Add((entry.Property, entry, instance));
                    extensions[entry.Property] = instance.GetValue(); // write back
                }
            }
            else if(allowEmpty)
            {
                // Create instance with empty value for relation reference
                matched.Add((entry.Property, entry, instance));
            }
        }

        // 2. Topological sort: dependencies first
        // Build lookup for quick index access
        Dictionary<string, int> indexMap = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < matched.Count; i++)
            indexMap[matched[i].name] = i;

        int count = matched.Count;
        bool[] visited = new bool[count];
        bool[] onStack = new bool[count];
        List<T> sorted = new(count);

        for (int i = 0; i < count; i++)
        {
            if (!visited[i]) Visit(i);
        }

        return sorted;

        void Visit(int i)
        {
            if (onStack[i] || visited[i]) return;

            onStack[i] = true;

            // Visit required dependencies
            if (matched[i].entry.Depends is { } depends)
            {
                foreach (string dep in depends)
                {
                    if (indexMap.TryGetValue(dep, out int depIdx))
                        Visit(depIdx);
                    else
                        throw new InvalidOperationException($"Constraint '{matched[i].name}' depends on '{dep}', which is not found in the extensions properties.");
                }
            }

            // Visit optional dependencies (same ordering, but presence is not required)
            if (matched[i].entry.OptionDepends is { } optionDepends)
            {
                foreach (string dep in optionDepends)
                {
                    if (indexMap.TryGetValue(dep, out int depIdx))
                        Visit(depIdx);
                }
            }

            onStack[i] = false;
            visited[i] = true;
            sorted.Add(matched[i].instance);
        }
    }

    static ConcurrentDictionary<string, Type> propertyMap = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(PropertyType? schema)
    {
        return schema?.ToSchema().With(new PropertySchema
        {
            Property = schema.Property,
            ValueType = schema.ValueType,
            Depends = schema.Depends,
            OptionDepends = schema.OptionDepends,
            ForSchemas = schema.ForSchemas,
            ForValues = schema.ForValues,
            IncludeArray = schema.IncludeArray,
        });
    }

    #endregion

    #region Utility

    static bool MatchSchemaValueType(AnySchemaType schemaType, ValueSchemaType valueType, bool? includeArray = null)
    {
        if (includeArray == true && schemaType is ArrayType arr)
        {
            if (valueType == Enum.ValueSchemaType.Array) return true;
            return arr.ElementSchemaType != null 
                ? MatchSchemaValueType(arr.ElementSchemaType, valueType, includeArray)
                : false;
        }

        return valueType switch
        {
            Enum.ValueSchemaType.All => true,
            Enum.ValueSchemaType.Scalar => schemaType is ScalarType,
            Enum.ValueSchemaType.Enum => schemaType is EnumType,
            Enum.ValueSchemaType.Struct => schemaType is StructType,
            Enum.ValueSchemaType.Array => schemaType is ArrayType,
            Enum.ValueSchemaType.Json => schemaType is JsonType,
            Enum.ValueSchemaType.Number => schemaType is ScalarType scalar && scalar.IsNumber,
            Enum.ValueSchemaType.Int => schemaType is ScalarType scalar && scalar.IsInt,
            Enum.ValueSchemaType.Single => schemaType is ScalarType scalar && scalar.IsSingle,
            Enum.ValueSchemaType.Double => schemaType is ScalarType scalar && scalar.IsDouble,
            Enum.ValueSchemaType.Bool => schemaType is ScalarType scalar && scalar.IsBool,
            Enum.ValueSchemaType.Char => schemaType is ScalarType scalar && scalar.IsChar,
            Enum.ValueSchemaType.String => schemaType is ScalarType scalar && scalar.IsString,
            Enum.ValueSchemaType.Date => schemaType is ScalarType scalar && scalar.IsDate,
            Enum.ValueSchemaType.Year => schemaType is ScalarType scalar && scalar.IsYear,
            Enum.ValueSchemaType.YearMonth => schemaType is ScalarType scalar && scalar.IsYearMonth,
            Enum.ValueSchemaType.FullDate => schemaType is ScalarType scalar && scalar.IsFullDate,
            Enum.ValueSchemaType.IntEnum => schemaType is EnumType enumType && enumType.ValueType == EnumValueType.Int,
            Enum.ValueSchemaType.FlagsEnum => schemaType is EnumType enumType && enumType.ValueType == EnumValueType.Flags,
            Enum.ValueSchemaType.StringEnum => schemaType is EnumType enumType && enumType.ValueType == EnumValueType.String,
            _ => false
        };
    }

    #endregion
}
