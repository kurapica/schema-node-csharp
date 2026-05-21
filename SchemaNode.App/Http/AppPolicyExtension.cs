using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.App.Http;

/// <summary>
/// Policy authorization extension methods for the App layer.
/// Provides AuthorizeAsync helpers for AppType, AppFieldType, AppWorkflowType.
/// Uses <see cref="IAppSchemaContext"/> for function invocation so the API layer
/// does not depend directly on Core's CompileContext internals.
/// </summary>
public static class AppPolicyExtension
{
    /// <summary>
    /// Evaluates the given policy items against the App schema context and returns whether access is authorized.
    /// </summary>
    public static async Task<bool> AuthorizeAsync(
        this IAppSchemaContext appContext,
        IEnumerable<PolicyItem> items,
        bool silent = false)
    {
        bool authorized = true;

        foreach (PolicyItem item in items)
        {
            try
            {
                JsonNode? result = await appContext.CallFunctionAsync(item.Evaluator, new JsonArray());
                authorized = result is JsonValue val && val.TryGetValue(out bool r) && r;
            }
            catch (Exception ex)
            {
                appContext.LogError(ex, "Policy evaluation error for {policy}", item.Evaluator);
                authorized = false;
            }

            if (authorized  && item.Combine == PolicyCombine.OrElse)  break;
            if (!authorized && item.Combine == PolicyCombine.AndAlso) break;
        }

        if (!silent && !authorized) throw new UnauthorizedAccessException();
        return authorized;
    }

    /// <summary>Authorize with a single evaluator function name.</summary>
    public static async Task<bool> AuthorizeAsync(
        this IAppSchemaContext appContext,
        string evaluator,
        bool silent = false)
    {
        try
        {
            JsonNode? result = await appContext.CallFunctionAsync(evaluator, new JsonArray());
            bool authorized = result is JsonValue val && val.TryGetValue(out bool r) && r;
            if (!silent && !authorized) throw new UnauthorizedAccessException();
            return authorized;
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            appContext.LogError(ex, "Policy evaluation error for {evaluator}", evaluator);
            if (!silent) throw new UnauthorizedAccessException();
            return false;
        }
    }

    /// <summary>Authorize an AppType with the given policy scope.</summary>
    public static Task<bool> AuthorizeAsync(
        this IAppSchemaContext context, AppType app, PolicyScope scope, bool silent = false)
        => context.AuthorizeAsync(app.GetAuthPolicies(scope), silent);

    /// <summary>Authorize an AppFieldType with the given policy scope.</summary>
    public static Task<bool> AuthorizeAsync(
        this IAppSchemaContext context, AppFieldType field, PolicyScope scope, bool silent = false)
        => context.AuthorizeAsync(field.GetAuthPolicies(scope), silent);

    /// <summary>Authorize an AppWorkflowType with the given policy scope.</summary>
    public static Task<bool> AuthorizeAsync(
        this IAppSchemaContext context, AppWorkflowType workflow, PolicyScope scope, bool silent = false)
        => context.AuthorizeAsync(workflow.GetAuthPolicies(scope), silent);
}
