using System.Threading;
using System.Threading.Tasks;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.WPF.View;

/// <summary>
/// Validates the subtree edited by a row editor before its values are written back. Core validates
/// each node asynchronously when its value changes, so the dialog must force the whole subtree
/// through validation instead of trusting a possibly pending result.
/// </summary>
internal static class RowEditorValidation
{
    /// <summary>Validates <paramref name="node"/> and every data node below it.</summary>
    public static async Task<bool> ValidateTreeAsync(DataNode node, CancellationToken cancellationToken = default)
    {
        await ValidateNodeAsync(node, cancellationToken).ConfigureAwait(false);
        return node.IsValid;
    }

    private static async Task ValidateNodeAsync(DataNode node, CancellationToken cancellationToken)
    {
        // Hidden fields are not part of the submitted row, so their subtree is skipped
        // instead of forcing required constraints on values the user cannot reach.
        if (!node.VisibleInTree) return;

        await node.ValidateAsync(cancellationToken).ConfigureAwait(false);

        if (node is ArrayNode array)
        {
            foreach (IValueAccess item in array)
                if (item is DataNode child)
                    await ValidateNodeAsync(child, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (node is StructNode structure)
        {
            foreach ((StructFieldType _, IValueAccess value) in structure.GetFields())
                if (value is DataNode child)
                    await ValidateNodeAsync(child, cancellationToken).ConfigureAwait(false);
        }
    }
}
