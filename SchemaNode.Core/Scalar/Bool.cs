using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the bool scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_BOOL)]
public class Bool: IScalarType<bool>;