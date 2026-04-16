using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Property.Constraint;

/// <summary>
/// Limit the enum's cascade level
/// </summary>
[SchemaProperty([SchemaType.StructField], [ValueSchemaType.Enum], includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class CascadeProperty : SchemaProperty<long>, IConstraintProperty
{
    public async Task<bool?> ValidateEnumAsync(SchemaContext context, EnumTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        var effectiveValue = overrideValue?.ToValue<long>() ?? Value;
        if (effectiveValue <= 0 || node.IsEmpty) return null;
        EnumType? enumType = node.SchemaType as EnumType;
        if (enumType?.Cascade == null || enumType.Cascade.Length <= effectiveValue) return null;

        var access = await enumType.LoadEnumAccessListAsync(context, node.Value!.ToString()!, noSubList: true, withSubList: false);
        return access.Length <= effectiveValue;
    }
}