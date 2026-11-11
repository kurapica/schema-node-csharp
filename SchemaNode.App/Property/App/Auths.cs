using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Drawing;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using AppType = SchemaNode.Runtime.AppType;
using NamespaceType = SchemaNode.Runtime.NamespaceType;
using NodeType = SchemaNode.Runtime.NodeType;
using SchemaType = SchemaNode.Property.Core.SchemaType;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace SchemaNode.Property.App;

/// <summary>
/// The auths property, used for declaring the auth policies for the node, the value is an array of policy items which will be evaluated at runtime to determine if the access is allowed or not. It supports multiple policies with different scopes and combine methods, and the evaluation result will be combined based on the combine method.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE, SCHEMA_KIND_APP, SCHEMA_KIND_APP_FIELD, SCHEMA_KIND_APP_WORKFLOW)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(Auths)}")]
public class Auths : Property<PolicyItem[]>, ILoadableProperty, IErrorProvider, ITypeRefProperty
{
    public string? Error { get; set; }
    
    public async Task LoadAsync(SchemaContext context, Runtime.ValueType? ownerType = null, CancellationToken token = default)
    {
        if (Value == null) return;
        foreach (PolicyItem item in Value)
        {
            item.Function = await context.GetNodeTypeAsync<FunctionType>(item.Evaluator, token: token).ConfigureAwait(false);
            if (item.Function is { Args.Length: 0 } && item.Function.Return.IsAssignableTo(context.System.Bool)) continue;
            Error ??= AppErrorCodes.APP_POLICY_EVALUATOR_NOT_VALID;
        }
    }

    public IEnumerable<string> GetRefTypes()
    {
        if (Value == null) yield break;
        foreach (var item in Value)
            yield return item.Evaluator;
    }
}

/// <summary>
/// Represents the policy validation function type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.evaluator")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF)] // All parameters will be fetched from context
public class EvaluatorType : ValidFuncType;

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
/// The black list resolver for the policy scope, it restricts the data scopes when the policy is not declared on an app or app field.
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_ENUM)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<PropertyValueType>(NS_SYSTEM_BOOL)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.policy.scopeblacklist")]
[Meta<Static>(true)]
[Meta<InVisible>(true)]
[Meta<Alias>("policyScopeResolver")]
public class PolicyScopeResolver : Property<bool>
{
    /// <inheritdoc/>
    public override void Effect(IValueAccess target)
    {
        // defer the effect, so the parent chain is available after the node is attached
        _ = Task.Run(async () =>
        {
            await Task.Yield();
            IValueAccess? curr = target;
            while (curr is DataNode node)
            {
                string? kind = node.Type.GetProperty<Attach>()?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(kind))
                {
                    curr = node.Parent;
                    continue;
                }

                if (!kind.Equals(SCHEMA_KIND_APP, StringComparison.OrdinalIgnoreCase) &&
                    !kind.Equals(SCHEMA_KIND_APP_FIELD, StringComparison.OrdinalIgnoreCase))
                {
                    target.SetPropertyValue<BlackList, object[]>(
                    [
                        nameof(PolicyScope.DataCreate).ToCamelCase(),
                        nameof(PolicyScope.DataRead).ToCamelCase(),
                        nameof(PolicyScope.DataUpdate).ToCamelCase(),
                        nameof(PolicyScope.DataDelete).ToCamelCase(),
                    ], target, null);
                }
                break;
            }
        });
    }
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
    [Meta<PolicyScopeResolver>(true)]
    public required PolicyScope Scope { get; set; }

    /// <summary>
    /// The policy evaluator
    /// </summary>
    [Meta<SchemaType>(typeof(EvaluatorType))]
    public required string Evaluator { get; set; }

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public FunctionType? Function { get; set; }
}

/// <summary>
/// The policy evaluator result cache
/// </summary>
internal class PolicyEvaluatorResult
{
    /// <summary>
    /// The evaluation result cache
    /// </summary>
    public readonly ConcurrentDictionary<string, bool> Result = [];
}

public static class PolicyExtensions
{
    /// <summary>
    /// Gets the authentication policies with the scope for node schema
    /// </summary>
    private static IEnumerable<PolicyItem> GetAuthPolicies(this NodeSchema schema, SchemaContext context, PolicyScope scope)
    {
        // Try parent auth policy items first
        NamespaceType? parent = !string.IsNullOrWhiteSpace(schema.Namespace) 
            ? context.GetNodeTypeAsync<NamespaceType>(schema.Namespace).ConfigureAwait(false).GetAwaiter().GetResult() 
            : schema.Name.Equals(NS_SYSTEM, StringComparison.OrdinalIgnoreCase)
                ? null 
                : context.System.Self;
        if (parent != null)
        {
            foreach (PolicyItem item in parent.GetAuthPolicies(context, scope))
                yield return item;
        }
        
        PolicyItem[]? auths = schema.GetProperty<Auths>()?.Value;
        if (auths == null) yield break;
        foreach (var item in auths.Where(p => p.Scope == scope))
            yield return item;
    }
    
    /// <summary>
    /// Gets the authentication policies with the scope for node type
    /// </summary>
    private static IEnumerable<PolicyItem> GetAuthPolicies(this NodeType type, SchemaContext context, PolicyScope scope)
    {
        // Try parent auth policy items first
        NamespaceType? parent = type.Name.Equals(NS_SYSTEM, StringComparison.OrdinalIgnoreCase)
            ? null
            : type.Namespace ?? context.System.Self;
        if (parent != null)
        {
            foreach (var item in parent.GetAuthPolicies(context, scope))
                yield return item;
        }

        PolicyItem[]? auths = type.GetProperty<Auths>()?.Value;
        if (auths == null) yield break;
        foreach (var item in auths.Where(p => p.Scope == scope))
            yield return item;
    }

    
    /// <summary>
    /// Gets the authentication policies with the scope
    /// </summary>
    private static IEnumerable<PolicyItem> GetAuthPolicies(this AppType appType, SchemaContext context, PolicyScope scope)
    {
        AppType? parent = appType.Name.Equals(NS_SYSTEM, StringComparison.OrdinalIgnoreCase) 
                              ? null 
                              : appType.Container ?? context.GetAppTypeAsync(NS_SYSTEM).ConfigureAwait(false).GetAwaiter().GetResult();
        if (parent != null)
        {
            foreach (var item in parent.GetAuthPolicies(context, scope))
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
    private static IEnumerable<PolicyItem> GetAuthPolicies(this AppFieldType appFieldType, SchemaContext context, PolicyScope scope)
    {
        // Application policy first
        foreach (var i in appFieldType.Application.GetAuthPolicies(context, scope)) yield return i;

        // self policies
        PolicyItem[]? auths = appFieldType.GetProperty<Auths>()?.Value;
        if (auths == null) yield break;
        foreach (var i in auths.Where(p => p.Scope == scope))
            yield return i;
    }
    
    /// <summary>
    /// Gets the authentication policies with the scope
    /// </summary>
    private static IEnumerable<PolicyItem> GetAuthPolicies(this AppWorkflowType appWorkflow, SchemaContext context, PolicyScope scope)
    {
        // Application policy first
        foreach (var i in appWorkflow.Application.GetAuthPolicies(context, scope)) yield return i;

        // self policies
        PolicyItem[]? auths = appWorkflow.GetProperty<Auths>()?.Value;
        if (auths == null) yield break;
        foreach (var i in auths.Where(p => p.Scope == scope))
            yield return i;
    }

    
    private record EvaluatorInfo(bool global, string? app, string? field);

    extension(SchemaContext context)
    {
        private async Task<EvaluatorInfo> GetEvaluatorInfo(FunctionType func, CancellationToken token = default)
        {
            if (func.TryGetRuntimeFuncCache(typeof(EvaluatorInfo), out EvaluatorInfo? info)) return info!;
            
            // Check special function access
            foreach(var exp in func.Exps)
            {
                switch (exp.FuncNode?.BaseName)
                {
                    case $"{NS_SYSTEM_DATA_APP}.{nameof(SystemAppData.getfield)}":
                    case $"{NS_SYSTEM_DATA_APP}.{nameof(SystemAppData.get)}":
                    {
                        var app = exp.Args?.ElementAt(0)?.Value?.ToValue<string>();
                        var field = exp.Args?.ElementAt(1)?.Value?.ToValue<string>();
                        if (!string.IsNullOrWhiteSpace(app) && !string.IsNullOrWhiteSpace(field))
                        {
                            var appType = await context.GetAppTypeAsync(app, token: token);
                            if (appType is null || appType.ScopeType == SchemaNode.Enum.AppScopeType.SystemLevel) break;
                            info = new EvaluatorInfo(false, app, field);
                            return func.SetRuntimeFuncCache(typeof(EvaluatorInfo), info)!;
                        }
                        break;
                    }
                }
            }
            info = new EvaluatorInfo(true, null, null);
            return func.SetRuntimeFuncCache(typeof(EvaluatorInfo), info)!;
        }

        /// <summary>
        /// authorize the schema with the policy scope
        /// </summary>
        private async Task<bool> AuthorizeAsync(IEnumerable<PolicyItem> items, bool silent = false, CancellationToken token = default)
        {
            // if no policy, authorized
            bool authorized = true;

            // cache the evaluation result in context
            PolicyEvaluatorResult cache = context.GetOrAddContextItem<PolicyEvaluatorResult>();

            // check policies in order
            foreach (PolicyItem item in items)
            {
                authorized = false;
                try
                {
                    FunctionType? func = await context.GetNodeTypeAsync<FunctionType>(item.Evaluator, token: token);
                    if (func is null) continue;

                    EvaluatorInfo info = await context.GetEvaluatorInfo(func, token);
                    string key = string.Empty;
                    if (info.global)
                        key = item.Evaluator;
                    else if (context.GetContextItem<Access>() is { } access && info.app!.Equals(access.App, StringComparison.OrdinalIgnoreCase))
                        key = $"{item.Evaluator}:{access.App}:{info.field}:{access.Target}";
                    else
                        break;

                    // The result should be the same for the same evaluator in one context
                    if (!cache.Result.TryGetValue(key, out authorized))
                    {
                        bool? result = await func.CallAsync<bool>(context, [], token: token).ConfigureAwait(false);
                        if (result.HasValue)
                        {
                            authorized = result.Value;
                            cache.Result[key] = authorized;
                        }
                    }
                
                    if (authorized)
                        break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch(Exception ex)
                {
                    context.LogError(ex, "Policy evaluation error for {policy}", item.Evaluator);
                }
            }

            // throw if not authorized
            if (!silent && !authorized) throw new UnauthorizedAccessException();
            return authorized;
        }

        /// <summary>
        /// Authorize with the evaluator function name
        /// </summary>
        public async Task<bool> AuthorizeAsync(string evaluator, bool silent = false, CancellationToken token = default)
        {
            // cache the evaluation result in context
            PolicyEvaluatorResult cache = context.GetOrAddContextItem<PolicyEvaluatorResult>();

            FunctionType? func = await context.GetNodeTypeAsync<FunctionType>(evaluator, token: token);
            if (func is null) return false;

            EvaluatorInfo info = await context.GetEvaluatorInfo(func, token);
            string key = string.Empty;
            if (info.global)
                key = evaluator;
            else if (context.GetContextItem<Access>() is { } access && info.app!.Equals(access.App, StringComparison.OrdinalIgnoreCase))
                key = $"{evaluator}:{access.App}:{info.field}:{access.Target}";
            else
                return false;

            // The result should be the same for the same evaluator in one context
            if (!cache.Result.TryGetValue(key, out var authorized))
            {
                try
                {
                    bool? result = await func.CallAsync<bool>(context, [], token: token).ConfigureAwait(false);
                    if (result.HasValue)
                    {
                        authorized = result.Value;
                        cache.Result[key] = authorized;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    context.LogError(ex, "Policy evaluation error for {evaluator}", evaluator);
                }
            }

            // throw if not authorized
            if (!silent && !authorized) throw new UnauthorizedAccessException();
            return authorized;
        }

        /// <summary>
        /// Authorize with the evaluator function
        /// </summary>
        public Task<bool> AuthorizeAsync(FunctionType evaluator, bool silent = false, CancellationToken token = default) 
            => context.AuthorizeAsync(evaluator.Name, silent, token);

        /// <summary>
        /// Authorize the node schema with the policy scope
        /// </summary>
        public Task<bool> AuthorizeAsync(NodeSchema schema, PolicyScope scope, bool silent = false, CancellationToken token = default)
            => context.AuthorizeAsync(schema.GetAuthPolicies(context, scope), silent, token);

        /// <summary>
        /// Authorize the schema type with the policy scope
        /// </summary>
        public Task<bool> AuthorizeAsync(NodeType type, PolicyScope scope, bool silent = false, CancellationToken token = default)
            => context.AuthorizeAsync(type.GetAuthPolicies(context, scope), silent, token);

        /// <summary>
        /// Authorize the app type with the policy scope
        /// </summary>
        public Task<bool> AuthorizeAsync(AppType app, PolicyScope scope, bool silent = false, CancellationToken token = default)
            => context.AuthorizeAsync(app.GetAuthPolicies(context, scope), silent, token);

        /// <summary>
        /// Authorize the app field with the policy scope
        /// </summary>
        public Task<bool> AuthorizeAsync(AppFieldType appField, PolicyScope scope, bool silent = false, CancellationToken token = default)
            => context.AuthorizeAsync(appField.GetAuthPolicies(context, scope), silent, token);

        /// <summary>
        /// Authorize the app workflow with the policy scope
        /// </summary>
        public Task<bool> AuthorizeAsync(AppWorkflowType appWorkflow, PolicyScope scope, bool silent = false, CancellationToken token = default)
            => context.AuthorizeAsync(appWorkflow.GetAuthPolicies(context, scope), silent, token);
    }
}