using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Components.Property.Presentation;

/// <summary>
/// Define the description for a schema node.
/// </summary>
[SchemaProperty([SchemaType.StructField, SchemaType.App, SchemaType.AppField, SchemaType.AppWorkflow], [ValueSchemaType.All])]
public class DescProperty : SchemaProperty<LocaleString>
{
    public override void Init(SchemaContext context)
    {
        SystemLocale.Translate(Value);
    }
}