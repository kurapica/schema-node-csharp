using SchemaNode.Node;
using SchemaNode.Schema;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "int" schema kind (Int, Year).
/// Year is validated as either a plain long integer or extracted from a date string.
/// </summary>
public sealed class IntType : ScalarType
{
    /// <inheritdoc/>
    public override bool IsIndexable => true;

    /// <inheritdoc/>
    public override IValueAccess Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new IntNode { Type = this, Parent = parent, PropertyProvider = propertyProvider ?? this };
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<IntProperty>()?.Value;
}
