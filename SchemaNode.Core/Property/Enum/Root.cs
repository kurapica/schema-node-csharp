using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Enum;

/// <summary>
/// Restrict the enum value to be a descendant of the specified root value.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ENUM_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_ENUM}.{nameof(Root)}")]
public class Root: Property<string>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        string? nodeValue = node.GetValue<string>();
        if (string.IsNullOrWhiteSpace(Value) || string.IsNullOrWhiteSpace(nodeValue)) return null;
        if (Value.Equals(nodeValue)) return true;

        var access = await (node.Type as Runtime.EnumType)!.GetEnumEntryAccessAsync(context, nodeValue);
        return access.Any(a =>Value.Equals(a.Entry?.Value));
    }
}