using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The struct field has a stack-up limit, When calculating the up limit, add the original value.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(StackUpLimit)}")]
[Relation<Visible, Call>(nameof(StackUpLimit), $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isschemakind)}", $"@{nameof(StructFieldSchema.Type)}", false, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL)]
public class StackUpLimit : Property<bool>;