using SchemaNode.Attribute;
using SchemaNode.Components.Property.Constraint;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory constraint schema representation
/// </summary>
public sealed class ConstraintType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The constraint property name, such as "uplimit", "lowlimit", "pattern", etc.
    /// </summary>
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The constraint value type
    /// </summary>
    public string ValueType { get; internal set; } = string.Empty;

    /// <summary>
    /// The required constraint names that this constraint depends on
    /// </summary>
    public string[]? Depends { get; internal set; }

    /// <summary>
    /// The optional constraint names that this constraint depends on
    /// </summary>
    public string[]? OptionDepends { get; internal set; }

    /// <summary>
    /// The schema types that this constraint applies to
    /// </summary>
    public SchemaType[]? For { get; internal set; }

    #endregion

    #region Ref

    /// <summary>
    /// The value schema type
    /// </summary>
    public AnySchemaType? ValueSchemaType { get; internal set; }

    #endregion

    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Constraint;

    /// <inheritdoc />
    public override bool IsUsed => true;

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        ConstraintSchema? constraint = schema.Constraint;

        // Data
        Property = constraint?.Name ?? string.Empty;
        ValueType = constraint?.ValueType ?? string.Empty;
        Depends = constraint?.Depends;
        OptionDepends = constraint?.OptionDepends;
        For = constraint?.For;

        if (string.IsNullOrWhiteSpace(Property) || constraint == null) Status = SchemaNodeStatus.NoDefinition;

        ValueSchemaType = !string.IsNullOrWhiteSpace(ValueType) ? await context.GetSchemaTypeAsync(ValueType) : null;
        if (ValueSchemaType == null) Status = SchemaNodeStatus.ConstraintHasWrongValueType;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false)
    {
        return null;
    }

    #endregion

    #region Static Feature

    /// <summary>
    /// Generate the constraint schema from the given constraint type
    /// </summary>
    public static NodeSchema[] GenerateSystemConstraint(Type type, string? ns = null)
    {
        Type? constraintInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Constraint<>));
        if (constraintInterface == null) return [];

        // Get the value type T from Constraint<T>
        Type valueType = constraintInterface.GetGenericArguments()[0];

        // Get the constraint name
        ConstraintAttribute? attr = type.GetCustomAttribute<ConstraintAttribute>();
        string name;
        if (attr != null)
        {
            name = attr.Name;
        }
        else
        {
            name = type.Name;
            if (name.EndsWith("Constraint", StringComparison.OrdinalIgnoreCase))
                name = name[..^"Constraint".Length];
        }
        name = name.ToCamelCase();

        string typeName = $"{NS_SYSTEM_CONSTRAINT}.{name.ToLower()}";

        NodeSchema constraintSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Constraint,
            Display = attr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Constraint = new ConstraintSchema
            {
                Property = name,
                ValueType = valueType.GetSchemaType(true, ns),
                Depends = attr?.Depends,
                OptionDepends = attr?.OptionDepends,
                For = attr?.For,
            }
        };

        if (SystemLocale.HasLocales)
            SystemLocale.Translate(constraintSchema.Display, constraintSchema.Name);

        ConstraintTypeMap.TryAdd(name, new ConstraintEntry(type, type.GetProperty(nameof(Constraint<>.Value))!, attr?.Depends, attr?.OptionDepends, attr?.For));
        return [constraintSchema];
    }

    /// <summary>
    /// Build constraint instances whose property name appears in <paramref name="additional"/>,
    /// filtered by <paramref name="schemaType"/>, with values deserialized from the matching
    /// <see cref="JsonElement"/>. The returned list is topologically sorted by
    /// <see cref="ConstraintAttribute.Depends"/> (dependencies first).
    /// </summary>
    public static IEnumerable<IConstraint> GetConstraints(SchemaContext context, SchemaType schemaType, Dictionary<string, JsonElement> additional)
    {
        // 1. Collect matching constraints
        List<(string name, ConstraintEntry entry, IConstraint instance)> matched = [];

        foreach ((string name, ConstraintEntry entry) in ConstraintTypeMap)
        {
            // Filter by For (null means all schema types)
            if (entry.For is { Length: > 0 } && !entry.For.Contains(schemaType))
                continue;

            // Property name must exist in additional (case-insensitive)
            if (!additional.TryGetValue(name, out JsonElement element))
                continue;

            // Create instance and deserialize value
            if (Activator.CreateInstance(entry.ImplType) is not IConstraint instance)
                continue;

            // Get T from Constraint<T> and set Value
            object? value = element.Deserialize(entry.prop.PropertyType, Extension.GetJsonOptions(false));
            instance.Name = name;
            entry.prop.SetValue(instance, value);

            matched.Add((name, entry, instance));
        }

        // 2. Topological sort: dependencies first
        // Build lookup for quick index access
        Dictionary<string, int> indexMap = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < matched.Count; i++)
            indexMap[matched[i].name] = i;

        int count = matched.Count;
        bool[] visited = new bool[count];
        bool[] onStack = new bool[count];
        List<IConstraint> sorted = new(count);

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
                        throw new InvalidOperationException($"Constraint '{matched[i].name}' depends on '{dep}', which is not found in the additional properties.");
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

    private sealed record ConstraintEntry(Type ImplType, PropertyInfo prop, string[]? Depends, string[]? OptionDepends, SchemaType[]? For);

    private static readonly ConcurrentDictionary<string, ConstraintEntry> ConstraintTypeMap = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(ConstraintType? schema)
    {
        return schema?.ToSchema().With(new ConstraintSchema
        {
            Property = schema.Property,
            ValueType = schema.ValueType,
            Depends = schema.Depends,
            OptionDepends = schema.OptionDepends,
            For = schema.For,
        });
    }

    #endregion
}
