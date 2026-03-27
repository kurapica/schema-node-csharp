using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Components.Property.Presentation;

/// <summary>
/// Define the error message for a schema node when validation fails.
/// </summary>
[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.All])]
public class ErrorProperty : SchemaProperty<LocaleString>
{    
    public override void Init(SchemaContext context)
    {
        SystemLocale.Translate(Value);
    }
}