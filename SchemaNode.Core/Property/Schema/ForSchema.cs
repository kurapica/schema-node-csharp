using SchemaNode.Utility;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Declare what schema types the property is defined for
/// </summary>
public sealed class ForSchema : Property<string[]>
{
    /// <inheritdoc/>
    public override void SetValue<T>(T value)
    {
        base.SetValue(value);
        Value = Value?.Select(x => x.GetSchemaKind()).ToArray();
        if (Value == null || Value.Length == 0) throw new Exception("ForSchema property must have at least one value");
    }
}
