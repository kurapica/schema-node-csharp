using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The entry source holder of entry source property
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(EntrySourceProvider)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
[Relation<Valid, Relation.Assign>($"{nameof(EntrySourceProvider)}.{nameof(FuncCall.Func)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"{NS_SYSTEM_LIST}<{NS_SYSTEM_ENTRY_ACCESS}>", true)]
public class EntrySourceProvider: FuncCallProperty;
