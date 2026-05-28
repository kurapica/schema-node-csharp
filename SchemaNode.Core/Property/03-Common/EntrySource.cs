using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

[Meta<ForSchema>(SCHEMA_KIND_INT, SCHEMA_KIND_DECIMAL, SCHEMA_KIND_STRING)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.entrysource")]
[Meta<PropertyValueType>(typeof(FuncType))]
public class EntrySource : Property<string>;