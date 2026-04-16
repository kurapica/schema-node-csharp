using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the string scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_STRING)]
public class String: IScalarType<string>;

/// <summary>
/// Represents the char scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_CHAR)]
[Meta<UplimitStringProperty>(1)]
[Meta<LowLimitStringProperty>(1)]
public class Char: String;

/// <summary>
/// Represents the GUID scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_GUID)]
public class Guid: String, IScalarType<Guid>;

/// <summary>
/// Represents the language scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_LANGUAGE)]
[Meta<UplimitStringProperty>(LANGUAGE_MAX_LEN)]
public class Language: String;

/// <summary>
/// Represents the identifier scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_IDENTIFIER)]
[Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
public class Identifier: String;