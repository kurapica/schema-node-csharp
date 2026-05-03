using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar.Schema;

#region Schema

/// <summary>
/// Represents the property that can be used on the schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM}.property")]
[Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
public class Property: String;

#endregion

#region Schema Type

/// <summary>
/// Represents the namespace type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.anytype")]
[Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
public class AnyType: String;

/// <summary>
/// Represents the namespace type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.namespace")]
public class NamespaceType: String;

/// <summary>
/// Represents the non-array type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.elementtype")]
public class ElementType : AnyType;

/// <summary>
/// Represents the value type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.valuetype")]
public class ValueType : AnyType;

/// <summary>
/// Represents the bool scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.bool")]
public class BoolType : AnyType;

/// <summary>
/// Represents the string scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.string")]
public class StringScalarType : AnyType;

/// <summary>
/// Represents the date scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.date")]
public class DateScalarType : AnyType;

/// <summary>
/// Represents the decimal scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.decimal")]
public class DecimalType : AnyType;

/// <summary>
/// Represents the int scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.int")]
public class IntScalarType : AnyType;

/// <summary>
/// Represents the object (any-value) scalar type — actual type is resolved by Relation
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.object")]
public class AnyObjectType : AnyType;

/// <summary>
/// Represents the enum type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.enum")]
public class EnumType: AnyType;

/// <summary>
/// Represents the struct type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.struct")]
public class StructType: AnyType;

/// <summary>
/// Represents the array type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.array")]
public class ArrayType: AnyType;

/// <summary>
/// Represents the function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.func")]
public class FuncType: AnyType;

/// <summary>
/// Represents the property type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.property")]
public class PropertyType: AnyType;

#endregion

#region Schema Function Type

/// <summary>
/// Represents the validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.valid")]
public class ValidFuncType: FuncType;

/// <summary>
/// Represents the union validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.unionvalid")]
public class UnionValidFuncType : FuncType;

#endregion
