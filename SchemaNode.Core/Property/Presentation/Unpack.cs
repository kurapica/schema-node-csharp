using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// Declare the field with object type used as unpack field
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
public class Unpack : Property<bool>;