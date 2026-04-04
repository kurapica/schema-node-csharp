using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Property;

/// <summary>
/// Declare a convert property for property schema
/// </summary>
[SchemaProperty([SchemaType.Property])]
public sealed class ConvertProperty : SchemaProperty<bool>
{
}

/// <summary>
/// The interface for convert property components that can be attached to recognizer part schemas.
/// It defines bidirectional conversion logic for recognizer parsing (string → type) and emitting (type → string).
/// Each convert property takes the original value and the accumulated result from previous property processing,
/// and returns the converted result.
/// </summary>
[SchemaPropertyKind(nameof(ConvertProperty))]
public interface IConvertProperty : IProperty
{
    /// <summary>
    /// Parse: convert for the parse direction (string → type).
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this call.</param>
    string? Parse(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null) => result ?? value;

    /// <summary>
    /// Async version of <see cref="Parse"/>. Override this for async convert processing.
    /// </summary>
    Task<string?> ParseAsync(SchemaContext context, string value, string result, AnySchemaNode? overrideValue = null) => Task.FromResult(Parse(context, value, result, overrideValue));

    /// <summary>
    /// Emit: convert for the emit direction (type → string).
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this call.</param>
    string? Emit(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null) => result ?? value;

    /// <summary>
    /// Async version of <see cref="Emit"/>. Override this for async convert processing.
    /// </summary>
    Task<string?> EmitAsync(SchemaContext context, string value, string? result, AnySchemaNode? overrideValue = null) => Task.FromResult(Emit(context, value, result, overrideValue));

    /// <summary>
    /// Parse: directly produce a structured node from input string.
    /// Used by function-based parsers that produce nodes directly.
    /// </summary>
    AnySchemaNode? ParseNode(SchemaContext context, string value, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="ParseNode"/>.
    /// </summary>
    Task<AnySchemaNode?> ParseNodeAsync(SchemaContext context, string value, AnySchemaNode? overrideValue = null) => Task.FromResult(ParseNode(context, value, overrideValue));

    /// <summary>
    /// Emit: directly produce a string from a structured node.
    /// Used by function-based formatters that take nodes directly.
    /// </summary>
    string? EmitNode(SchemaContext context, AnySchemaNode value, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="EmitNode"/>.
    /// </summary>
    Task<string?> EmitNodeAsync(SchemaContext context, AnySchemaNode value, AnySchemaNode? overrideValue = null) => Task.FromResult(EmitNode(context, value, overrideValue));
}
