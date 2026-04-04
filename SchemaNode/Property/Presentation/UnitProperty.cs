using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// Define the unit of a scalar type, such as "ms", "kg", "m/s", etc. 
/// This is a presentation property that can be used for documentation and UI rendering, 
/// and does not affect the validation logic. It can be applied to scalar types and struct fields with scalar types.
/// </summary>
[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.Number])]
public class UnitProperty: SchemaProperty<LocaleString>
{
    public override void Init(SchemaContext context)
    {
        SystemLocale.Translate(Value);
    }
}