using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function.Reflect;

[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_PROPERTY)]
public static class Property
{
    /// <summary>
    /// Gets the properties attached to kinds
    /// </summary>
    public static async Task<EntryAccess<string>[]> getkindproperties(SchemaContext context, [Meta<SchemaType>(NS_SYSTEM_SCHEMA_KIND)] params string[] kinds)
    {
        var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = new List<Entry<string>>();
        foreach(var kind in kinds)
        {
            if (string.IsNullOrWhiteSpace(kind)) continue;
            foreach (var propType in context.Runtime.GetSchemaKindPropertyTypes(kind))
            {
                if (propType.GetSchemaType() is not { } schemaType || string.IsNullOrWhiteSpace(schemaType)) continue;

                string name = propType.GetPropertyName();
                if (!string.IsNullOrWhiteSpace(name) && nameSet.Add(name))
                {
                    var type = await context.GetNodeTypeAsync<PropertyType>(schemaType);
                    if (type != null)
                    {
                        if (type.GetProperty<Static>()?.Value == true) continue;
                        var entry = new Entry<string> { Value = type.Property };
                        entry.SetProperty<Display, LocaleString>(type.GetProperty<Display>()?.Value ?? type.Property);
                        properties.Add(entry);
                    }
                }
            }
        }
        return [new EntryAccess<string>
        {
            Children = properties.ToArray()
        }];
    }

    /// <summary>
    /// Gets the property value type of the given kind
    /// </summary>
    public static async Task<string?> getkindpropvaluetype(SchemaContext context, string property, [Meta<SchemaType>(NS_SYSTEM_SCHEMA_KIND)] params string[] kinds)
    {
        foreach (var kind in kinds)
        {
            if (string.IsNullOrWhiteSpace(kind)) continue;
            foreach (var propType in context.Runtime.GetSchemaKindPropertyTypes(kind))
            {
                if (propType.GetSchemaType() is not { } schemaType || string.IsNullOrWhiteSpace(schemaType)) continue;

                string name = propType.GetPropertyName();
                if (property.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    var type = await context.GetNodeTypeAsync<PropertyType>(schemaType);
                    if (type != null) return type.ValueType?.Name;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The property is a static property, which means it can't be used as relation property
    /// </summary>
    public static async Task<bool> isstatic(SchemaContext context, [Meta<SchemaType>(typeof(Schema.PropertyType))] string type)
    {
        var propertyType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.PropertyType>(type) : null;
        return propertyType?.GetProperty<Static>()?.Value ?? false;
    }

    /// <summary>
    /// The property is stackable property, which means an owner can have multi properties of the property type
    /// </summary>
    public static async Task<bool> isstackable(SchemaContext context, [Meta<SchemaType>(typeof(Schema.PropertyType))] string type)
    {
        var propertyType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.PropertyType>(type) : null;
        return propertyType?.GetProperty<Stackable>()?.Value ?? false;
    }

    /// <summary>
    /// The property is non-static property, which means it can be used in relation
    /// </summary>
    public static async Task<bool> notstatic(SchemaContext context, [Meta<SchemaType>(typeof(Schema.PropertyType))] string type)
    {
        var propertyType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.PropertyType>(type) : null;
        return !(propertyType?.GetProperty<Static>()?.Value ?? false);
    }

    /// <summary>
    /// The property is non-stackable, the owner can have only one value of the property type
    /// </summary>
    public static async Task<bool> notstackable(SchemaContext context, [Meta<SchemaType>(typeof(Schema.PropertyType))] string type)
    {
        var propertyType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.PropertyType>(type) : null;
        return !(propertyType?.GetProperty<Stackable>()?.Value ?? false);
    }
    
    /// <summary>
    /// Gets the property value type
    /// </summary>
    public static async Task<string?> getvaluetype(SchemaContext context, 
        [Meta<SchemaType>(typeof(Schema.PropertyType))] string name,
        [Meta<SchemaType>(typeof(Schema.ValueType))] string? ownerType = null)
    {
        var prop = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync<Runtime.PropertyType>(name) : null;
        var typeName = prop?.ValueType?.Name;
        return typeName == NS_SYSTEM_OBJECT && !string.IsNullOrWhiteSpace(ownerType) ? ownerType : typeName;
    }

    /// <summary>
    /// The property is for the schema kind, which means it can be used in the schema kind
    /// </summary>
    public static async Task<bool> forschema(SchemaContext context, [Meta<SchemaType>(typeof(Schema.PropertyType))] string name, [Meta<SchemaType>(typeof(SchemaKind))] params string[] kinds)
    {
        var prop = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync<Runtime.PropertyType>(name) : null;
        return kinds.Any(kind => prop?.ForSchema(kind) ?? false);
    }

}