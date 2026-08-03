using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The entry list or white list only used as suggestion
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_STRUCT_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(AsSuggest)}")]
[Relation<Visible, Call>(nameof(AsSuggest), $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.isschemakind)}", $"@{nameof(StructFieldSchema.Type)}", true, SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Relation<InVisible, Call>(nameof(AsSuggest), $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.isempty)}", $"@{nameof(WhiteList)}")]
public class AsSuggest: Property<bool>;