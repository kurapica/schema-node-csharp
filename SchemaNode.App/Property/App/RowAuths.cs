using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.App;

/// <summary>
/// The row auths
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP_FIELD)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.{nameof(RowAuths)}")]
[Relation<Valid>($"${nameof(RowAuths)}.{nameof(RowPolicy.Filter)}", NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF, $"${nameof(AppFieldSchema.Type)}", true)]
public class RowAuths : Property<RowPolicy[]>, ILoadableProperty, INodeError
{
    public string? Error { get; set; }
    
    public async Task LoadAsync(SchemaContext context, Runtime.ValueType? ownerType = null)
    {
        if (Value == null) return;
        if (ownerType is Runtime.ArrayType arr) ownerType = arr.Element;
        
        foreach (RowPolicy item in Value)
        {
            item.EvaluatorFunc = await context.GetNodeTypeAsync<FunctionType>(item.Evaluator);
            if (item.EvaluatorFunc == null || item.EvaluatorFunc.Args.Length != 0 || !item.EvaluatorFunc.Return.IsAssignableTo(context.System.Bool))
            {
                Error ??= AppErrorCodes.APP_ROW_AUTH_EVALUATOR_NOT_VALID;
                continue;
            }
            item.FilterFunc = await  context.GetNodeTypeAsync<FunctionType>(item.Filter);
            if (item.FilterFunc is { Args.Length: 1 } && item.FilterFunc.Return.IsAssignableTo(context.System.Bool) && 
                (ownerType == null || (item.FilterFunc.Args[0].ValueType != null && item.FilterFunc.Args[0].ValueType!.IsAssignableTo(ownerType)))) continue;
            Error ??= AppErrorCodes.APP_ROW_AUTH_FILTER_NOT_VALID;
        }
    }
}

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
    public required string Filter { get; set; }

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
