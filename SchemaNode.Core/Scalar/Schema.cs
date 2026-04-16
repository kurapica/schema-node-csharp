using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Scalar.Schema;

#region Schema

/// <summary>
/// Represents the property that can be used on the schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.property")]
[Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
public class Property:  String;

#endregion

#region Schema Type

/// <summary>
/// Represents the namespace type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.anyvalue")]
public class AnyValue: String;

/// <summary>
/// Represents the namespace type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.anytype")]
[Meta<UplimitStringProperty>(PRIMARY_KEY_MAX_LEN)]
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
/// Represents the scalar type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_TYPE}.scalar")]
public class ScalarType : AnyType;

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
