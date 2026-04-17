using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Enum;

namespace SchemaNode.Property;

/// <summary>
/// The interface for constraint property components that can be attached to schemas. 
/// It defines the validation logic for the constraint rule. 
/// Each constraint property component should implement the Validate method for the applicable schema types, and return true if valid, false if invalid, null if not applicable.
/// </summary>
public interface IConstraintProperty: IProperty
{
    /// <summary>
    /// Validate the scalar type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateScalar(SchemaContext context, ScalarNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="ValidateScalar"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateScalarAsync(SchemaContext context, ScalarNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null) => Task.FromResult(ValidateScalar(context, node, parent, overrideValue));

    /// <summary>
    /// Validate the enum type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateEnum(SchemaContext context, EnumNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="ValidateEnum"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateEnumAsync(SchemaContext context, EnumNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null) => Task.FromResult(ValidateEnum(context, node, parent, overrideValue));

    /// <summary>
    /// Validate the struct type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateStruct(SchemaContext context, StructNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="ValidateStruct"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateStructAsync(SchemaContext context, StructNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null) => Task.FromResult(ValidateStruct(context, node, parent, overrideValue));

    /// <summary>
    /// Validate the array type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateArray(SchemaContext context, ArrayNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        if ((overrideValue == null && !HasValue) || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if (item is ScalarNode scalarTypeNode)
            {
                if (ValidateScalar(context, scalarTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is EnumNode enumTypeNode)
            {
                if (ValidateEnum(context, enumTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is StructNode structTypeNode)
            {
                if (ValidateStruct(context, structTypeNode, parent, overrideValue) == false)
                    return false;
            }
        }
        return null;
    }

    /// <summary>
    /// Async version of <see cref="ValidateArray"/>. Override this for async constraint validation.
    /// </summary>
    public virtual async Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayNode node, StructNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        if ((overrideValue == null && !HasValue) || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if (item is ScalarNode scalarTypeNode)
            {
                if (await ValidateScalarAsync(context, scalarTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is EnumNode enumTypeNode)
            {
                if (await ValidateEnumAsync(context, enumTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is StructNode structTypeNode)
            {
                if (await ValidateStructAsync(context, structTypeNode, parent, overrideValue) == false)
                    return false;
            }
        }
        return null;
    }
}