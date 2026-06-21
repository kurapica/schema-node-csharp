using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the bool scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_BOOL)]
[Meta<OfSchema>(SCHEMA_KIND_BOOL)]
public class Bool: IScalarType<bool>;