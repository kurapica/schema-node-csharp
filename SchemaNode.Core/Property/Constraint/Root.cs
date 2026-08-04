using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Restrict the enum value to be a descendant of the specified root value.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(Root)}")]
[Relation<Visible, Relation.Call>(nameof(Root), $"{NS_SYSTEM_SCHEMA_REFLECT_ENUM}.{nameof(SchemaNode.Function.Reflect.Enum.hascascade)}", $"@{nameof(StructFieldSchema.Type)}")]
[Relation<OverrideType, Relation.Call>(nameof(Root), $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(StructFieldSchema.Type)}")]
[Relation<Cascade, Relation.Call>(nameof(Root), $"{NS_SYSTEM_MATH}.{nameof(SystemMath.subtract)}", $"@{nameof(Cascade)}", 1L)]
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