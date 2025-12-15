using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace SchemaNode.Components;

public static class PolicyExtension
{
    /// <summary>
    /// authorize the schema with the policy scope
    /// </summary>
    static async Task<bool> AuthorizeAsync(this SchemaContext context, IEnumerable<PolicyItem> items, bool silent = false)
    {
        // if no policy, authorized
        bool authorized = true;

        // cache the evaluation result in context
        PolicyEvaluatorResult cache = context.GetOrCreateContextItem<PolicyEvaluatorResult>();

        // check policies in order
        foreach (PolicyItem item in items)
        {
            try
            {
                // The result should be the same for the same evaluator in one context
                if (!cache.Result.TryGetValue(item.Evaluator, out authorized))
                {
                    JsonNode? result = await context.CallFunctionAsync(item.Evaluator, new JsonArray());
                    if (result is JsonValue val && val.TryGetValue(out authorized))
                    {
                        cache.Result[item.Evaluator] = authorized;
                    }
                }
                
                if (authorized && item.Combine == PolicyCombine.OrElse)
                    break;
                if (!authorized && item.Combine == PolicyCombine.AndAlso)
                    break;
            }
            catch(Exception ex)
            {
                context.Logger.LogError(ex, "Policy evaluation error for {policy}", item.Evaluator);
            }
        }

        // throw if not authorized
        if (!silent && !authorized) throw new UnauthorizedAccessException();
        return authorized;
    }

    /// <summary>
    /// Authorize with the evaluator function name
    /// </summary>
    public static async Task<bool> AuthorizeAsync(this SchemaContext context, string evaluator, bool silent = false)
    {
        // cache the evaluation result in context
        PolicyEvaluatorResult cache = context.GetOrCreateContextItem<PolicyEvaluatorResult>();

        // The result should be the same for the same evaluator in one context
        if (!cache.Result.TryGetValue(evaluator, out var authorized))
        {
            try
            {
                JsonNode? result = await context.CallFunctionAsync(evaluator, new JsonArray());
                if (result is JsonValue val && val.TryGetValue(out authorized))
                    cache.Result[evaluator] = authorized;
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Policy evaluation error for {evaluator}", evaluator);
            }
        }

        // throw if not authorized
        if (!silent && !authorized) throw new UnauthorizedAccessException();
        return authorized;
    }

    /// <summary>
    /// Authorize with the evaluator function
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, FunctionType evaluator, bool silent = false) => AuthorizeAsync(context, evaluator.Name, silent);

    /// <summary>
    /// Authorize the schema type with the policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AnySchemeType type, PolicyScope scope, bool silent = false)
        => AuthorizeAsync(context, type.GetAuthPolicies(scope), silent);

    /// <summary>
    /// Authorize the app type with the policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AppType app, PolicyScope scope, bool silent = false)
        => AuthorizeAsync(context, app.GetAuthPolicies(scope), silent);

    /// <summary>
    /// Authorize the app field with the policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AppFieldType appField, PolicyScope scope, bool silent = false)
        => AuthorizeAsync(context, appField.GetAuthPolicies(scope), silent);

    /// <summary>
    /// Authorize the app workflow with the policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AppWorkflowType appWorkflow, PolicyScope scope, bool silent = false)
        => AuthorizeAsync(context, appWorkflow.GetAuthPolicies(scope), silent);
}

internal class PolicyEvaluatorResult
{
    /// <summary>
    /// The evaluation result cache
    /// </summary>
    public readonly ConcurrentDictionary<string, bool> Result = [];
}
