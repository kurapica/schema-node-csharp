using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function.Reflect;

[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_PROPERTY)]
public static class Property
{
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
    public static async Task<bool> forschema(SchemaContext context, [Meta<SchemaType>(typeof(Schema.PropertyType))] string name, [Meta<SchemaType>(typeof(SchemaKind))] string kind)
    {
        var prop = !string.IsNullOrWhiteSpace(name) ? await context.GetNodeTypeAsync<Runtime.PropertyType>(name) : null;
        return prop?.ForSchema(kind) ?? false;
    }

}