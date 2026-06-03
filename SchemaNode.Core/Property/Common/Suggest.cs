using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

[Meta<Alias>("suggest")]
[Meta<ForSchema>(SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(StringSuggest)}")]
public class StringSuggest : Property<Entry<string>[]>;

[Meta<Alias>("suggest")]
[Meta<ForSchema>(SCHEMA_KIND_INT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(IntSuggest)}")]
public class IntSuggest : Property<Entry<long>[]>;