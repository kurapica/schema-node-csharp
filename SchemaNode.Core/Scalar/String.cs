using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar;

/// <summary>
/// Represents the string scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_STRING)]
public class String: IScalarType<string>;

/// <summary>
/// Represents the char scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_CHAR)]
[Meta<UplimitString>(1)]
[Meta<LowLimitString>(1)]
[Meta<ClrEquivalent>(typeof(char))]
public class Char: String;

/// <summary>
/// Represents the GUID scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_GUID)]
public class Guid: String, IScalarType<System.Guid>;

/// <summary>
/// Represents the language scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_LANGUAGE)]
[Meta<UplimitString>(LANGUAGE_MAX_LEN)]
public class Language: String;

/// <summary>
/// Represents the identifier scalar value type
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_IDENTIFIER)]
[Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
public class Identifier: String;
