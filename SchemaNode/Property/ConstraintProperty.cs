using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Enum;

namespace SchemaNode.Property;

/// <summary>
/// Declare a constraint property for property schema
/// </summary>
[SchemaProperty([SchemaType.Property])]
public sealed class ConstraintProperty : SchemaProperty<bool>
{
}

/// <summary>
/// The interface for constraint property components that can be attached to schemas. 
/// It defines the validation logic for the constraint rule. 
/// Each constraint property component should implement the Validate method for the applicable schema types, and return true if valid, false if invalid, null if not applicable.
/// </summary>
[SchemaPropertyKind(nameof(ConstraintProperty))]
public interface IConstraintProperty: IProperty
{
    /// <summary>
    /// Validate the scalar type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateScalar(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="ValidateScalar"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateScalarAsync(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null) => Task.FromResult(ValidateScalar(context, node, parent, overrideValue));

    /// <summary>
    /// Validate the enum type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateEnum(SchemaContext context, EnumTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="ValidateEnum"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateEnumAsync(SchemaContext context, EnumTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null) => Task.FromResult(ValidateEnum(context, node, parent, overrideValue));

    /// <summary>
    /// Validate the struct type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateStruct(SchemaContext context, StructTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null) => null;

    /// <summary>
    /// Async version of <see cref="ValidateStruct"/>. Override this for async constraint validation.
    /// </summary>
    public virtual Task<bool?> ValidateStructAsync(SchemaContext context, StructTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null) => Task.FromResult(ValidateStruct(context, node, parent, overrideValue));

    /// <summary>
    /// Validate the array type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    /// <param name="overrideValue">Optional override value from a relation, replaces the property's own Value for this validation call.</param>
    public virtual bool? ValidateArray(SchemaContext context, ArrayTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        if ((overrideValue == null && !HasValue) || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if (item is ScalarTypeNode scalarTypeNode)
            {
                if (ValidateScalar(context, scalarTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is EnumTypeNode enumTypeNode)
            {
                if (ValidateEnum(context, enumTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is StructTypeNode structTypeNode)
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
    public virtual async Task<bool?> ValidateArrayAsync(SchemaContext context, ArrayTypeNode node, StructTypeNode? parent = null, AnySchemaNode? overrideValue = null)
    {
        if ((overrideValue == null && !HasValue) || node.IsEmpty) return null;
        foreach (var item in node)
        {
            if (item is ScalarTypeNode scalarTypeNode)
            {
                if (await ValidateScalarAsync(context, scalarTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is EnumTypeNode enumTypeNode)
            {
                if (await ValidateEnumAsync(context, enumTypeNode, parent, overrideValue) == false)
                    return false;
            }
            else if (item is StructTypeNode structTypeNode)
            {
                if (await ValidateStructAsync(context, structTypeNode, parent, overrideValue) == false)
                    return false;
            }
        }
        return null;
    }
}