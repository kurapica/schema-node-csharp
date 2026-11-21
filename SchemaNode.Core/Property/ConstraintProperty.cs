using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Struct;

namespace SchemaNode.Property;

/// <summary>
/// Stores the localized error message declared by each constraint property schema.
/// <para>
/// The schema generator records the key while building the property type, but runtime constraint
/// instances are created on demand from the CLR class and have no reference back to their
/// <c>PropertyType</c>. Keeping the message here, keyed by the constraint class, mirrors the
/// TypeScript implementation, which stores it on the class constructor (<c>ctor.error</c>).
/// </para>
/// </summary>
internal static class ConstraintErrorMessages
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, LocaleString> Messages = new();

    /// <summary>
    /// Records the schema-declared error message for the constraint class.
    /// </summary>
    public static void Register(Type constraintType, LocaleString? message)
    {
        if (message is null || string.IsNullOrEmpty(message.Key)) return;
        Messages[constraintType] = message;
    }

    /// <summary>
    /// Gets the schema-declared error message for the constraint class, if any.
    /// </summary>
    public static LocaleString? Get(Type constraintType)
        => Messages.TryGetValue(constraintType, out LocaleString? message) ? message : null;
}

/// <summary>
/// The interface for constraint property components that can be attached to schemas. 
/// It defines the validation logic for the constraint rule. 
/// Each constraint property component should implement the Validate method for the applicable schema types, and return true if valid, false if invalid, null if not applicable.
/// </summary>
public interface IConstraintProperty : IProperty
{
    /// <summary>
    /// Ges the error message for node
    /// <para>
    /// Mirrors schema-node-core's constraint property: format the schema-declared message with the
    /// violated node's display (or name) and the constraint value. When the message cannot be
    /// translated, return <see langword="null"/> so the node falls back to <c>VALUE_NOT_VALID</c>
    /// instead of exposing a raw locale key.
    /// </para>
    /// </summary>
    public virtual string? GetErrorMessage(IValueAccess node)
    {
        LocaleString? error = ConstraintErrorMessages.Get(GetType());
        if (error is null || node is not DataNode dataNode) return null;

        object label = (object?)dataNode.GetPropertyValue<Display, LocaleString>()
            ?? dataNode.GetPropertyValue<Name, string>()
            ?? dataNode.Name
            ?? dataNode.Type.Name;
        string message = error.Format(dataNode.Context, label, GetValue<object>() ?? string.Empty);
        return string.Equals(message, error.Key, StringComparison.Ordinal) ? null : message;
    }

    /// <summary>
    /// Validate the data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? Validate(SchemaContext context, IValueAccess node)
    {
        return node switch
        {
            EnumNode enumNode => ValidateEnum(context, enumNode),
            IntNode intNode => ValidateInt(context, intNode),
            StringNode stringNode => ValidateString(context, stringNode),
            DecimalNode numericNode => ValidateNumeric(context, numericNode),
            DateNode dateNode => ValidateDate(context, dateNode),
            StructNode structNode => ValidateStruct(context, structNode),
            ArrayNode arrayNode => ValidateArray(context, arrayNode),
            _ => null
        };
    }

    /// <summary>
    /// Async version of <see cref="Validate"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateAsync(SchemaContext context, IValueAccess node, CancellationToken token = default)
    {
        return node switch
        {
            EnumNode enumNode => ValidateEnumAsync(context, enumNode, token),
            IntNode intNode => ValidateIntAsync(context, intNode, token),
            StringNode stringNode => ValidateStringAsync(context, stringNode, token),
            DecimalNode numericNode => ValidateNumericAsync(context, numericNode, token),
            DateNode dateNode => ValidateDateAsync(context, dateNode, token),
            StructNode structNode => ValidateStructAsync(context, structNode, token),
            ArrayNode arrayNode => ValidateArrayAsync(context, arrayNode, token),
            _ => Task.FromResult<bool?>(null)
        };
    }

    #region Enum validation

    /// <summary>
    /// Validate the enum data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateEnum(SchemaContext context, EnumNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateEnum"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node, CancellationToken token = default) => Task.FromResult(ValidateEnum(context, node));

    #endregion

    #region Scalar validation

    #region Int valdiation

    /// <summary>
    /// Validate the int data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateInt(SchemaContext context, IntNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateInt"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateIntAsync(SchemaContext context, IntNode node, CancellationToken token = default) => Task.FromResult(ValidateInt(context, node));

    #endregion

    #region String validation

    /// <summary>
    /// Validate the string data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateString(SchemaContext context, StringNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateInt"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateStringAsync(SchemaContext context, StringNode node, CancellationToken token = default) => Task.FromResult(ValidateString(context, node));

    #endregion

    #region Numberic validation

    /// <summary>
    /// Validate the numeric data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateNumeric(SchemaContext context, DecimalNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateNumeric"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateNumericAsync(SchemaContext context, DecimalNode node, CancellationToken token = default) => Task.FromResult(ValidateNumeric(context, node));

    #endregion

    #region Date validation

    /// <summary>
    /// Validate the date data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateDate(SchemaContext context, DateNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateDate"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateDateAsync(SchemaContext context, DateNode node, CancellationToken token = default) => Task.FromResult(ValidateDate(context, node));

    #endregion

    #endregion

    #region Struct valdiation

    /// <summary>
    /// Validate the struct data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateStruct(SchemaContext context, StructNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateStruct"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateStructAsync(SchemaContext context, StructNode node, CancellationToken token = default) => Task.FromResult(ValidateStruct(context, node));

    #endregion

    #region Array validation

    /// <summary>
    /// Validate the array data node with sync mode
    /// </summary>
    public virtual bool? ValidateArray(SchemaContext context, ArrayNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if (Validate(context, item) == false)
                return false;
        }
        return null;
    }

    /// <summary>
    /// Validate the array data node with async mode
    /// </summary>
    public virtual async Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node, CancellationToken token = default)
    {
        if (!HasValue || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if ((await ValidateAsync(context, item, token).ConfigureAwait(false)) == false)
                return false;
        }
        return null;
    }

    #endregion
}