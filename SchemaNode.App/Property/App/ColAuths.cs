using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using StructType = SchemaNode.Runtime.StructType;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SchemaNode.Property.App;

[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(ColAuths)}")]
[Relation<EntrySource, Assign>($"{nameof(ColAuths)}.{nameof(ColPolicy.Name)}", $"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.getsubentries)}", $"@{nameof(AppFieldSchema.Type)}")]
public class ColAuths : Property<ColPolicy[]>, ILoadableProperty, IErrorProvider
{
    public string? Error { get; set; }
    
    public async Task LoadAsync(SchemaContext context, Runtime.ValueType? ownerType = null)
    {
        if (Value == null) return;
        if (ownerType is ArrayType arr) ownerType = arr.Element;
        foreach (ColPolicy item in Value)
        {
            if (ownerType != null)
            {
                if ((ownerType as StructType)?.GetField(item.Name)?.Type == null)
                {
                    Error ??= AppErrorCodes.APP_COL_AUTH_FIELD_NOT_FOUND;
                    continue;
                }
            }
            
            List<FunctionType> evaluators = [];
            foreach (string eva in item.Evaluators)
            {
                FunctionType? func = await context.GetNodeTypeAsync<FunctionType>(eva);
                if (func == null)
                {
                    Error ??= AppErrorCodes.APP_COL_AUTH_EVALUATOR_NOT_VALID;
                    break;
                }
                evaluators.Add(func);
            }
            item.Functions = evaluators.ToArray();
        }
    }
}

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

public static class ColAuthsExtensions
{
    /// <summary>
    /// Gets the field authentication policies with the scope
    /// </summary>
    public static IEnumerable<string> GetColPolicies(this AppFieldType appFieldType, string fieldName)
    {
        ColPolicy[]? colPolicies = appFieldType.GetProperty<ColAuths>()?.Value;
        ColPolicy? item = colPolicies?.FirstOrDefault(i => i.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (item == null || item.Evaluators.Length == 0) yield break;
        foreach (var evaluator in item.Evaluators)
            yield return evaluator;
    }
}