using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Common;

/// <summary>
/// The row auths
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(RowAuths)}")]
[Relation<Valid>($"${nameof(RowAuths)}.{nameof(RowPolicy.Filter)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF, $"${nameof(AppFieldSchema.Type)}", true)]
public class RowAuths : Property<RowPolicy[]>;

/// <summary>
/// The row policy item
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.row")]
public sealed class RowPolicy
{
    /// <summary>
    /// The policy evaluator, if true will use the filter
    /// </summary>
    [Meta<SchemaType>(typeof(EvaluatorType))]
    public required string Evaluator { get; set; }

    /// <summary>
    /// The row filter function
    /// </summary>
    [Meta<SchemaType>(typeof(ValidFuncType))]
    public string? Filter { get; set; }

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? EvaluatorFunc { get; set; }

    /// <summary>
    /// The function type of the filter
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? FilterFunc { get; set; }
}
