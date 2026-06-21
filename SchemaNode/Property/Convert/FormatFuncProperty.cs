using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Convert;

/// <summary>
/// The fully qualified name of a function that converts a typed value to its string representation.
/// Signature: (value) → string.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart], schemaType: NS_SYSTEM_SCHEMA_TYPE_FUNC)]
public class FormatFuncProperty : SchemaProperty<string>, IConvertProperty, ITypeRefProperty
{
    /// <summary>
    /// The resolved function type for format
    /// </summary>
    internal FunctionType? FuncType { get; set; }

    /// <inheritdoc/>
    public async Task<string?> EmitNodeAsync(SchemaContext context, AnySchemaNode value, AnySchemaNode? overrideValue = null)
    {
        string? func = overrideValue?.ToValue<string>() ?? Value;
        if (string.IsNullOrWhiteSpace(func)) return null;
        FuncType ??= await context.GetSchemaTypeAsync<FunctionType>(func);
        if (FuncType == null) return null;
        return await FuncType.CallAsync<string>(context, [value]);
    }
}
