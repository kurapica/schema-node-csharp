using SchemaNode.Context;
using SchemaNode.Node;

namespace SchemaNode.Components.Property.Constraint;

public interface IConstraint: IProperty
{
    #region Abstract

    /// <summary>
    /// Validate the scalar type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    virtual bool? Validate(SchemaContext context, ScalarTypeNode node) => null;

    /// <summary>
    /// Validate the enum type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    virtual bool? Validate(SchemaContext context, EnumTypeNode node) => null;

    /// <summary>
    /// Validate the array type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    virtual bool? Validate(SchemaContext context, ArrayTypeNode node) => null;

    /// <summary>
    /// Validate the struct type node with the constraint rule. Return true if valid, false if invalid, null if not applicable.
    /// </summary>
    virtual bool? Validate(SchemaContext context, StructTypeNode node) => null;

    #endregion
}

/// <summary>
/// The interface for constraint components that can be attached to schemas, such like uplimit, lowlimit, pattern, etc.
/// </summary>
public class Constraint<T>: IProperty<T>, IConstraint
{
    /// <summary>
    /// The property name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The property value
    /// </summary>
    public T? Value { get; set; }
}
