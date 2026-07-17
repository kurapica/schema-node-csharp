using SchemaNode.Context;
using SchemaNode.Node;

namespace SchemaNode.Property;

/// <summary>
/// The interface for constraint property components that can be attached to schemas. 
/// It defines the validation logic for the constraint rule. 
/// Each constraint property component should implement the Validate method for the applicable schema types, and return true if valid, false if invalid, null if not applicable.
/// </summary>
public interface IConstraintProperty : IProperty
{
    /// <summary>
    /// Validate the data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? Validate(SchemaContext context, DataNode node)
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
    public virtual Task<bool?> ValidateAsync(SchemaContext context, DataNode node) => Task.FromResult(Validate(context, node));

    #region Enum validation

    /// <summary>
    /// Validate the enum data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateEnum(SchemaContext context, EnumNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateEnum"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node) => Task.FromResult(ValidateEnum(context, node));

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
    public virtual Task<bool?> ValidateIntAsync(SchemaContext context, IntNode node) => Task.FromResult(ValidateInt(context, node));

    #endregion

    #region String validation

    /// <summary>
    /// Validate the string data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateString(SchemaContext context, StringNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateInt"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateStringAsync(SchemaContext context, StringNode node) => Task.FromResult(ValidateString(context, node));

    #endregion

    #region Numberic validation

    /// <summary>
    /// Validate the numeric data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateNumeric(SchemaContext context, DecimalNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateNumeric"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateNumericAsync(SchemaContext context, DecimalNode node) => Task.FromResult(ValidateNumeric(context, node));

    #endregion

    #region Date validation

    /// <summary>
    /// Validate the date data node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    public virtual bool? ValidateDate(SchemaContext context, DateNode node) => null;

    /// <summary>
    /// Async version of <see cref="ValidateDate"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateDateAsync(SchemaContext context, DateNode node) => Task.FromResult(ValidateDate(context, node));

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
    public virtual Task<bool?> ValidateStructAsync(SchemaContext context, StructNode node) => Task.FromResult(ValidateStruct(context, node));

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
    public virtual async Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node)
    {
        if (!HasValue || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if ((await ValidateAsync(context, item)) == false)
                return false;
        }
        return null;
    }

    #endregion
}