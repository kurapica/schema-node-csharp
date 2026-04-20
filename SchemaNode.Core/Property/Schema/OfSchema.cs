using SchemaNode.Utility;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Declare the schema type of the given schema kinds
/// </summary>
public class OfSchema: Property<string[]>
{
    /// <inheritdoc/>
    public override void SetValue<T>(T value)
    {
        base.SetValue(value);
        Value = Value?.Select(x => x.GetSchemaKind()).ToArray();
        if (Value == null || Value.Length == 0) throw new Exception("OfSchema property must have at least one value");
    }
}
