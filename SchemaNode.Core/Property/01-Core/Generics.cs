using System.Collections.Immutable;
using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Property.Core;

[Meta<ForSchema>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ARRAY, SCHEMA_KIND_FUNCTION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<ReadOnly>(true)] // Can't be set in the designer, can only be generated from core types
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(Generics)}")]
public sealed class Generics: Property<GenericParameter[]>;

/// <summary>
/// The generic parameter declaration for node schema
/// </summary>
/// <param name="Name">The generic parameter name, e.g. "T"</param>
/// <param name="Compatibles">The compatible types for the generic parameter, nil allow all</param>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(GenericParameter)}")]
public sealed record GenericParameter(string Name, ImmutableArray<string>? Compatibles = null);