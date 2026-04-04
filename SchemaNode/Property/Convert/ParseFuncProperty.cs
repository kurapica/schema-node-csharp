using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Convert;

/// <summary>
/// The fully qualified name of a function that converts a string representation back to a typed value.
/// Signature: (string) → value.
/// </summary>
[SchemaProperty([SchemaType.RecognizerPart], schemaType: NS_SYSTEM_SCHEMA_TYPE_FUNC)]
public class ParseFuncProperty : SchemaProperty<string>, IConvertProperty, ITypeRefProperty
{
    /// <summary>
    /// The resolved function type for parse
    /// </summary>
    internal FunctionType? FuncType { get; set; }

    /// <inheritdoc/>
    public async Task<AnySchemaNode?> ParseNodeAsync(SchemaContext context, string value, AnySchemaNode? overrideValue = null)
    {
        string? func = overrideValue?.ToValue<string>() ?? Value;
        if (string.IsNullOrWhiteSpace(func)) return null;
        FuncType ??= await context.GetSchemaTypeAsync<FunctionType>(func);
        if (FuncType == null) return null;
        return await FuncType.CallAsync<AnySchemaNode>(context, [value]);
    }
}
