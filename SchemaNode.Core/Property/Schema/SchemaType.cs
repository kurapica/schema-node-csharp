using SchemaNode.Utility;

namespace SchemaNode.Property.Schema;

/// <summary>
/// Declare the schema type binding to the class, method, struct, enum, or the schema type that will be used on the property or field
/// </summary>
public sealed class SchemaType : Property<string>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
        => base.SetValue((value is Type type ? type.GetSchemaType() ?? throw new Exception($"Can't get the schema type of the given type {type.FullName}") : value?.TryConvertTo<string>())?.ToLowerInvariant());
}