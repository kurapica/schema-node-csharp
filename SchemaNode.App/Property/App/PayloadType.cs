using SchemaNode.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaNode.Property.App;

/// <summary>
/// The payload type for event and workflow
/// </summary>
public sealed class PayloadType: Property<string>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
        => base.SetValue((value is Type type ? type.GetSchemaType() ?? throw new Exception($"Can't get the schema type of the given type {type.FullName}") : value?.ConvertTo<string>())?.ToLowerInvariant());
}