using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using AppType = SchemaNode.Runtime.AppType;
using SchemaType = SchemaNode.Property.Core.SchemaType;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Property.App;

/// <summary>
/// The auths property, used for declaring the auth policies for the node, the value is an array of policy items which will be evaluated at runtime to determine if the access is allowed or not. It supports multiple policies with different scopes and combine methods, and the evaluation result will be combined based on the combine method.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE, SCHEMA_KIND_APP, SCHEMA_KIND_APP_FIELD, SCHEMA_KIND_APP_WORKFLOW)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.{nameof(Auths)}")]
public class Auths : Property<PolicyItem[]>, ILoadableProperty, INodeError
{
    public string? Error { get; set; }
    
    public async Task LoadAsync(SchemaContext context, Runtime.ValueType? ownerType = null)
    {
        if (Value == null) return;
        foreach (PolicyItem item in Value)
        {
            item.Function = await context.GetNodeTypeAsync<FunctionType>(item.Evaluator);
            if (item.Function is { Args.Length: 0 } && item.Function.Return.IsAssignableTo(context.System.Bool)) continue;
            Error ??= AppErrorCodes.APP_POLICY_EVALUATOR_NOT_VALID;
        }
    }
}

/// <summary>
/// Represents the policy validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.evaluator")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF)] // All parameters will be fetched from context
public class EvaluatorType : ValidFuncType;

/// <summary>
/// The policy combine
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.combine")]
public enum PolicyCombine
{
    /// <summary>
    /// auth1 && auth2
    /// </summary>
    AndAlso = 1,
    
    /// <summary>
    /// auth1 || auth2
    /// </summary>
    OrElse = 2,
}

/// <summary>
/// The policy scope
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.scope")]
public enum PolicyScope
{
    /// <summary>
    /// Create Schema
    /// </summary>
    SchemaCreate = 1,
    
    /// <summary>
    /// Read Schema
    /// </summary>
    SchemaRead,
    
    /// <summary>
    /// Update Schema
    /// </summary>
    SchemaUpdate,
    
    /// <summary>
    /// Delete Schema
    /// </summary>
    SchemaDelete,
    
    /// <summary>
    /// Create App Data
    /// </summary>
    DataCreate,
    
    /// <summary>
    /// Read App Data
    /// </summary>
    DataRead,
    
    /// <summary>
    /// Update App Data
    /// </summary>
    DataUpdate,
    
    /// <summary>
    /// Delete App Data
    /// </summary>
    DataDelete,

    /// <summary>
    /// Execute Function
    /// </summary>
    FuncExecute,
}

/// <summary>
/// The policy item schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.item")]
public sealed class PolicyItem
{
    /// <summary>
    /// The policy scope
    /// </summary>
    public required PolicyScope Scope { get; set; }

    /// <summary>
    /// The policy evaluator
    /// </summary>
    [Meta<SchemaType>(typeof(EvaluatorType))]
    public required string Evaluator { get; set; }

    /// <summary>
    /// The policy combine method
    /// </summary>
    public required PolicyCombine Combine { get; set; }

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? Function { get; set; }
}

public static class PolicyExtensions
{
    /// <summary>
    /// Gets the authentication policies with the scope
    /// </summary>
    public static IEnumerable<PolicyItem> GetAuthPolicies(this AppType appType, PolicyScope scope)
    {
        // use system for root
        if (appType.Container == null)
        {
            if (appType.GetAppType(NS_SYSTEM) is {} system)
            {
                foreach (var item in system.GetAuthPolicies(scope))
                    yield return item;
            }
        }
        // system won't inherit auth from root app
        else if (!appType.Name.Equals(NS_SYSTEM))
        {
            foreach (var item in appType.Container.GetAuthPolicies(scope))
                yield return item;
        }

        PolicyItem[]? auths = appType.GetProperty<Auths>()?.Value;
        if (auths == null) yield break;
        foreach (var item in auths.Where(p => p.Scope == scope))
            yield return item;
    }

    /// <summary>
    /// Gets the authentication policies with the scope
    /// </summary>
    public static IEnumerable<PolicyItem> GetAuthPolicies(this AppFieldType appFieldType, PolicyScope scope)
    {
        // Application policy first
        foreach (var i in appFieldType.Application.GetAuthPolicies(scope)) yield return i;

        // self policies
        PolicyItem[]? auths = appFieldType.GetProperty<Auths>()?.Value;
        if (auths == null) yield break;
        foreach (var i in auths.Where(p => p.Scope == scope))
            yield return i;
    }

}