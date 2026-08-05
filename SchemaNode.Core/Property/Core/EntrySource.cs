using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The entry source to provider cascade entry list
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING, SCHEMA_KIND_ENUM)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.entrysource")]
[Relation<Valid, Relation.Assign>($"{nameof(EntrySource)}.{nameof(FuncCall.Func)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"{NS_SYSTEM_LIST}<{NS_SYSTEM_ENTRY_ACCESS}>", true)]
public class EntrySource : FuncCallProperty;
