using SchemaNode.Context;
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
    public override IValueAccess Create(ISchemaContext? context, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders) => new StringNode(context, this, value, parent, propertyProviders);
    
    /// <inheritdoc />
    protected override ScalarSchema? GetScalarSchema() => GetProperty<StringProperty>()?.Value;
}
