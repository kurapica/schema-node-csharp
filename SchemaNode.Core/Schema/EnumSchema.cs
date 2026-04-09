using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaNode.Schema;

/// <summary>
/// The enum schema
/// </summary>
[Meta<SchemaKind>(nameof(EnumSchema))]
public sealed class EnumSchema: ExtensibleSchema
{
}

/// <summary>
/// Mark a property type whose values will be gathered as a new enum schema type
/// </summary>
public sealed class Enum : Property<string>;
