using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Relation;
using SchemaNode.Scalar;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Array;

/// <summary>
/// The data indexes
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_ARRAY, SCHEMA_KIND_ARRAY_DEFINE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.{nameof(Indexes)}")]
[Meta<Static>(true)]
public class Indexes : Property<DataIndex[]>, IConstraintProperty;


[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.index")]
public class DataIndex{
    /// <summary>
    /// The name of the index
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<PrimaryIndex>]
    public required string Name { get; set; }
    
    /// <summary>
    /// The fields of the index
    /// </summary>
    [Relation<BlackList, Call>($"{nameof(Fields)}.{ARRAY_ELEMENT}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", $"@{nameof(Fields)}.{ARRAY_PREVIOUS}")]
    public required string[] Fields { get; set; } = [];
    
    /// <summary>
    /// Is the index unique
    /// </summary>
    public bool IsUnique = false;
}