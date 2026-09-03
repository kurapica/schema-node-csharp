using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Property;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Core;

/// <summary>
/// The entry source to provider cascade entry list
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_CORE}.entrysource")]
[Relation<Valid, Relation.Assign>($"{nameof(EntrySource)}.{nameof(FuncCall.Func)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"{NS_SYSTEM_LIST}<{NS_SYSTEM_ENTRY_ACCESS}>", true)]
public class EntrySource : FuncCallProperty;

/// <summary>
/// The entry root argument
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_FUNC_ARG)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_CORE}.entryroot")]
[Meta<Static>(true)]
[Meta<ReadOnly>(true)]
[Meta<InVisible>(true)]
public class EntryRoot: Property<bool>;