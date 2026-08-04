using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The entry source to provider cascade entry list
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.entrysource")]
[Relation<Valid, Relation.Assign>($"{nameof(EntrySource)}.{nameof(FuncCall.Func)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"{NS_SYSTEM_LIST}<{NS_SYSTEM_ENTRY_ACCESS}>", true)]
public class EntrySource : FuncCallProperty;

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

/// <summary>
/// The entry source consumer of entry source property
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.{nameof(EntrySourceConsumer)}")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
public class EntrySourceConsumer: Property<Boolean>;