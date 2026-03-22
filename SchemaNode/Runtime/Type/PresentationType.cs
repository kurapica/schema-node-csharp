using SchemaNode.Attribute;
using SchemaNode.Components.Property.Constraint;
using SchemaNode.Components.Property.Presentation;
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
/// The in-memory presentation schema representation
/// </summary>
public sealed class PresentationType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The constraint property name, such as "uplimit", "lowlimit", "pattern", etc.
    /// </summary>
    public string Property { get; internal set; } = string.Empty;

    /// <summary>
    /// The presentation value type
    /// </summary>
    public string ValueType { get; internal set; } = string.Empty;

    /// <summary>
    /// The schema types that this presentation applies to
    /// </summary>
    public SchemaType[]? For { get; internal set; }

    #endregion

    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Presentation;

    /// <inheritdoc />
    public override bool IsUsed => true;

    #endregion

    #region Ref

    public AnySchemaType? ValueSchemaType { get; internal set; }

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        PresentationSchema? presentation = schema.Presentation;

        // Data
        Property = presentation?.Property ?? string.Empty;
        ValueType = presentation?.ValueType ?? string.Empty;
        For = presentation?.For;

        if (string.IsNullOrWhiteSpace(Property) || presentation == null) Status = SchemaNodeStatus.NoDefinition;

        ValueSchemaType = !string.IsNullOrWhiteSpace(ValueType) ? await context.GetSchemaTypeAsync(ValueType) : null;
        if (ValueSchemaType == null) Status = SchemaNodeStatus.PresentationHasWrongValueType;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false)
    {
        return null;
    }

    #endregion

    #region Static Feature

    /// <summary>
    /// Generate the presentation schema from the given presentation type
    /// </summary>
    public static NodeSchema[] GenerateSystemPresentation(Type type, string? ns = null)
    {
        Type? presentationInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Presentation<>));
        if (presentationInterface == null) return [];

        // Get the value type T from Presentation<T>
        Type valueType = presentationInterface.GetGenericArguments()[0];

        // Get the presentation name
        PresentationAttribute? attr = type.GetCustomAttribute<PresentationAttribute>();
        string name;
        if (attr != null)
        {
            name = attr.Name;
        }
        else
        {
            name = type.Name;
            if (name.EndsWith("Presentation", StringComparison.OrdinalIgnoreCase))
                name = name[..^"Presentation".Length];
        }
        name = name.ToCamelCase();

        string typeName = $"{NS_SYSTEM_PRESENTATION}.{name.ToLower()}";

        NodeSchema presentationSchema = new NodeSchema
        {
            Name = typeName,
            Type = SchemaType.Presentation,
            Display = attr?.Display ?? type.GetSummaryFromXmlDoc() ?? typeName,
            Presentation = new PresentationSchema
            {
                Property = name,
                ValueType = valueType.GetSchemaType(true, ns),
                For = attr?.For,
            }
        };

        if (SystemLocale.HasLocales)
            SystemLocale.Translate(presentationSchema.Display, presentationSchema.Name);

        PresentationTypeMap.TryAdd(name, new PresentationEntity(type, type.GetProperty(nameof(Presentation<>.Value))!, attr?.For));

        return [presentationSchema];
    }

    /// <summary>
    /// Build presentation instances whose property name appears in <paramref name="additional"/>,
    /// filtered by <paramref name="schemaType"/>, with values deserialized from the matching
    /// <see cref="JsonElement"/>.
    /// </summary>
    public static IEnumerable<IPresentation> GetPresentations(SchemaContext context, SchemaType schemaType, Dictionary<string, JsonElement> additional)
    {
        foreach ((string name, PresentationEntity entry) in PresentationTypeMap)
        {
            // Filter by For (null means all schema types)
            if (entry.For is { Length: > 0 } && !entry.For.Contains(schemaType))
                continue;

            // Property name must exist in additional (case-insensitive)
            if (!additional.TryGetValue(name, out JsonElement element))
                continue;

            // Create instance and deserialize value
            if (Activator.CreateInstance(entry.ImplType) is not IPresentation instance)
                continue;

            // Get T from Presentation<T> and set Value
            object? value = element.Deserialize(entry.prop.PropertyType, Extension.GetJsonOptions(false));
            instance.Name = name;
            entry.prop.SetValue(instance, value);

            yield return instance;
        }
    }

    private sealed record PresentationEntity(Type ImplType, PropertyInfo prop, SchemaType[]? For);

    private static readonly ConcurrentDictionary<string, PresentationEntity> PresentationTypeMap = new(StringComparer.OrdinalIgnoreCase);


    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(PresentationType? schema)
    {
        return schema?.ToSchema().With(new PresentationSchema
        {
            Property = schema.Property,
            ValueType = schema.ValueType,
            For = schema.For,
        });
    }

    #endregion
}
