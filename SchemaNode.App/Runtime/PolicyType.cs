using SchemaNode.Context;
using SchemaNode.Schema;
using static SchemaNode.App.AppStatus;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory policy schema representation.
/// Registered as the runtime type for the "policy" schema kind via [Meta&lt;NodeType&gt;(typeof(PolicyType))] on PolicySchema.
/// </summary>
public sealed class PolicyType : NodeType
{
    #region Properties

    /// <summary>
    /// The policy items
    /// </summary>
    public PolicyItem[] Items { get; private set; } = [];

    #endregion

    #region Overrides

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        PolicySchema? policy = GetPropertyValue<PolicySchema>();
        Items = policy?.Items ?? [];

        if (policy == null)
        {
            Error = PolicyWrongFunc;
            return;
        }

        foreach (PolicyItem item in Items)
        {
            FunctionType? func = !string.IsNullOrEmpty(item.Evaluator)
                ? await context.GetNodeTypeAsync(item.Evaluator) as FunctionType
                : null;

            if (func == null)
            {
                item.Status = PolicyWrongFunc;
                Error ??= PolicyWrongFunc;
            }
            else
            {
                func.AddUsedBy(this);
                item.Function = func;
            }
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        foreach (PolicyItem item in Items)
        {
            item.Function?.RemoveUsedBy(this);
            item.Function = null;
        }
        Error = null;
    }

    #endregion
}
