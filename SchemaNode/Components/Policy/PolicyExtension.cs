using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;

namespace SchemaNode.Components;

public static class PolicyExtension
{
    /// <summary>
    /// authorize the schema with the policy scope
    /// </summary>
    public static async Task<bool> AuthorizeAsync(this SchemaContext context, IEnumerable<PolicyItem> items, bool chkOnly = false)
    {
        // if no policy, authorized
        bool authorized = true;

        // check policies in order
        foreach (PolicyItem item in items)
        {
            try
            {
                JsonNode? result = await context.CallFunctionAsync(item.Evaluator, new JsonArray());
                if (result is JsonValue val && val.TryGetValue(out authorized))
                {
                    switch (authorized)
                    {
                        case true when item.Combine == PolicyCombine.OrElse:
                        case false when item.Combine == PolicyCombine.AndAlso:
                            break;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        // throw if not authorized
        if (!chkOnly && !authorized) throw new UnauthorizedAccessException();
        return authorized;
    }

    /// <summary>
    /// Authorize the schema type with the policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AnySchemeType type, PolicyScope scope, bool chkOnly = false)
        => AuthorizeAsync(context, type.GetAuthPolicies(scope), chkOnly);

    /// <summary>
    /// Authorize the app type with the policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AppType app, PolicyScope scope, bool chkOnly = false)
        => AuthorizeAsync(context, app.GetAuthPolicies(scope), chkOnly);

    /// <summary>
    /// Authorize the app field with the policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AppFieldType appField, PolicyScope scope, bool chkOnly = false)
        => AuthorizeAsync(context, appField.GetAuthPolicies(scope), chkOnly);

    /// <summary>
    /// Authorize the app field with the field name and policy scope
    /// </summary>
    public static Task<bool> AuthorizeAsync(this SchemaContext context, AppFieldType appField, string field, PolicyScope scope, bool chkOnly = false)
        => AuthorizeAsync(context, appField.GetAuthPolicies(field, scope), chkOnly);

}
