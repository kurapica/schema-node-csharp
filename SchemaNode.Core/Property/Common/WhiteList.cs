using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using SchemaNode.Relation;
using SchemaNode.Function;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;

namespace SchemaNode.Property.Common;

[Meta<ForSchema>(SCHEMA_KIND_ENUM, SCHEMA_KIND_STRING, SCHEMA_KIND_INT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_COMMON}.{nameof(WhiteList)}")]
[Relation<OverrideType, Call>(nameof(WhiteList), $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(SchemaNode.Function.Reflect.Array.getarraytype)}", TYPE_PROVIDER)]
[Relation<BlackList, Call>(nameof(WhiteList), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(BlackList)}")]
public class WhiteList : Property<object[]>, IConstraintProperty
{
    /// <inheritdoc/>
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty || node.Type.GetProperty<AsSuggest>()?.Value == true) return null;
        var accessList = await (node.Type as EnumType)!.GetEnumEntryAccessAsync(context, node.GetValue<string>());
        return accessList.Any(a => Value.Any(v => v.Equals(a.Entry?.Value)));
    }

    /// <inheritdoc/>
    public bool? ValidateInt(SchemaContext context, IntNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty || node.Type.GetProperty<AsSuggest>()?.Value == true) return null;
        return Value.Any(v => v.Equals(node.GetValue<string>()));
    }

    /// <inheritdoc/>
    public bool? ValidateString(SchemaContext context, StringNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty || node.Type.GetProperty<AsSuggest>()?.Value == true) return null;
        return Value.Any(v => v.Equals(node.GetValue<string>()));
    }
}