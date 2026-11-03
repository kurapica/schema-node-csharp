using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Property.Int;
using SchemaNode.Relation;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.WPF.Test.Type;

/// <summary>
/// Sample struct type registered as the schema type <c>test.minmax</c>.
/// </summary>
[Meta<SchemaType>("test.minmax")]
public class MinMax
{
    /// <summary>
    /// Min
    /// </summary>
    public int Min { get; set; }

    /// <summary>
    /// Max
    /// </summary>
    [Relation<LowLimitInt, Call>(nameof(Max), $"{NS_SYSTEM_INTRINSIC}.assign", $"@{nameof(Min)}")]
    public int Max { get; set; }
}
