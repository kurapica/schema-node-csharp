using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
namespace SchemaNode.Property.Convert;

/// <summary>
/// The type property defines the expected schema type of the value, and is used for relationship
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart], [ValueSchemaType.All], schemaType: NS_SYSTEM_SCHEMA_TYPE_RECOGNIZER)]
public class RecognizerProperty: SchemaProperty<string>, IConvertProperty, ITypeRefProperty
{
    public async Task<AnySchemaNode?> ParseNodeAsync(SchemaContext context, string value, AnySchemaNode? overrideValue = null)
    {
        string? name = overrideValue?.ToValue<string>() ?? Value;
        if (string.IsNullOrWhiteSpace(name)) return null;
        RecognizerType? type = await context.GetSchemaTypeAsync<RecognizerType>(name);
        if (type == null) return null;
        var result = await type.RecognizeAsync(context, value);
        return result.Success ? result.Value : null;
    }

    public async Task<string?> EmitNodeAsync(SchemaContext context, AnySchemaNode value, AnySchemaNode? overrideValue = null)
    {
        string? name = overrideValue?.ToValue<string>() ?? Value;
        if (string.IsNullOrWhiteSpace(name)) return null;
        RecognizerType? type = await context.GetSchemaTypeAsync<RecognizerType>(name);
        if (type == null) return null;
        return await type.EmitAsync(context, value);
    }
}