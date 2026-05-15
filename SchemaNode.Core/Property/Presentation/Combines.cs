using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Presentation;

/// <summary>
/// The data combine rules
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY)]
public class Combines : Property<DataCombine[]>;

/// <summary>
/// The data combine settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.combine")]
public sealed record DataCombine(string Field, DataCombineType Type = DataCombineType.Assign);
