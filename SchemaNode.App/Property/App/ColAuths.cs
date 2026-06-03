using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Function;
using SchemaNode.Property.Constraints;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.App;

[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.{nameof(RowAuths)}")]
[Relation<StringEntries>($"${nameof(ColAuths)}.{nameof(ColPolicy.Name)}", $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.getsubentries)}", $"${nameof(AppFieldSchema.Type)}")]
public class ColAuths : Property<ColPolicy[]>;

/// <summary>
/// The column policy item
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.col")]
public sealed class ColPolicy
{
    /// <summary>
    /// The struct field name
    /// </summary>
    public required string Name { get; set; } = string.Empty;

    /// <summary>
    /// The column access evaluators
    /// </summary>
    [Meta<SchemaType>(typeof(EvaluatorType))]
    public string[] Evaluators { get; set; } = [];

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType[] Functions { get; set; } = [];
}
