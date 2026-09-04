using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using EnumType = SchemaNode.Runtime.EnumType;

namespace SchemaNode.Property.Enum;

/// <summary>
/// Only allow leaf level enum values to be selected.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ENUM_USAGE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ENUM}.{nameof(LeafOnly)}")]
public class LeafOnly : Property<bool>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (!Value || node.IsEmpty) return null;
        EntryAccess<string>[]? val = (node.Type as EnumType) is { } enumType ? await enumType.GetEnumEntryAccessAsync(context, node.GetValue<string>()) : null;
        long? cascadeDepth = node.PropertyProvider?.GetProperty<CascadeDepth>()?.Value;
        if (val is null || val.Length == 0) return null;
        return val[^1].Entry?.HasChildren != true || cascadeDepth > 0 && val.Length == cascadeDepth.Value + 1;
    }
}
