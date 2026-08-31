using System.Collections.Immutable;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Property.Core;

[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<ReadOnly>(true)] // Can't be set in the designer, can only be generated from core types
[Meta<Static>(true)]
[Meta<InVisible>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(Generics)}")]
public sealed class Generics : Property<GenericParameter[]>
{
    public override void SetValue<TValue>(TValue value)
    {
        if (value is GenericParameter[] or JsonArray)
            base.SetValue(value);
        else if (value is object[] arr)
        {
            base.SetValue(arr.Select(v => v is GenericParameter gp ? gp : new GenericParameter(v.ToString()!)).ToArray());
        }
        else if (value is string)
        {
            base.SetValue(new[] { new GenericParameter(value.ToString()!) });
        }
    }
}

/// <summary>
/// The generic parameter declaration for node schema
/// </summary>
/// <param name="Name">The generic parameter name, e.g. "T"</param>
/// <param name="Compatibles">The compatible types for the generic parameter, nil allow all</param>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(GenericParameter)}")]
public sealed record GenericParameter(string Name, ImmutableArray<string>? Compatibles = null);