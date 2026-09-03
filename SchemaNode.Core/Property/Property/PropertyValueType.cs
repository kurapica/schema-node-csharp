using SchemaNode.Utility;

namespace SchemaNode.Property.Property;

/// <summary>
/// The value type of the property
/// </summary>
public class PropertyValueType: Property<string>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
        => base.SetValue((value is Type type ? type.GetSchemaType() ?? throw new Exception($"Can't get the schema type of the given type {type.FullName}") : value?.ConvertTo<string>())?.ToLowerInvariant());
}