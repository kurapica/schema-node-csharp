using SchemaNode.Context;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using SchemaNode.Relation;
using SchemaNode.Function;
using SchemaNode.Schema;

namespace SchemaNode.Property.Constraint;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(WhiteList)}")]
[Relation<Visible, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isschemakind)}", $"@{nameof(StructFieldSchema.Type)}", true, SCHEMA_KIND_ENUM, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Relation<OverrideType, Call>(NODE_SELF, $"{NS_SYSTEM_SCHEMA_REFLECT_ARRAY}.{nameof(SystemReflect.Array.getarraytype)}", $"@{nameof(StructFieldSchema.Type)}")]
public class WhiteList : Property<object[]>, IConstraintProperty
{
    /// <inheritdoc/>
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node)
    {
        if (Value == null || Value.Length == 0 || node.IsEmpty || node.Type.GetProperty<AsSuggest>()?.Value == true) return null;
        var accessList = await (node.Type as Runtime.EnumType)!.GetEnumEntryAccessAsync(context, node.GetValue<string>());
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