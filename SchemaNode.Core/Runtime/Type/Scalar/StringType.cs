using SchemaNode.Node;
using SchemaNode.Property.String;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// Runtime type for the "string" schema kind.
/// </summary>
public sealed class StringType : ScalarType
{
    /// <inheritdoc/>
    public override bool IsIndexable => GetProperty<UpLimitString>()?.GetValue<long>() < ENTITY_PRIMARY_KEY_MAX_LEN;

    /// <inheritdoc/>
    public override DataNode Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null) => new StringNode { Type = this, Parent = parent, PropertyProvider = propertyProvider ?? this };
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<StringProperty>()?.Value;
}
