using SchemaNode.Utility;

namespace SchemaNode.Property.Core;

/// <summary>
/// Declare the setting type for a schema kind
/// </summary>
public sealed class SchemaUsage : Property<string>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
        => base.SetValue((value is Type type ? type.GetSchemaType() ?? throw new Exception($"Can't get the schema type of the given type {type.FullName}") : value?.ConvertTo<string>())?.ToLowerInvariant());
}