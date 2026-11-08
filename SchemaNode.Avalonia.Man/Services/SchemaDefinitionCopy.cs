using SchemaNode.Property.Core;
using SchemaNode.Schema;

namespace SchemaNode.Avalonia.Man.Services;

/// <summary>
/// Creates the seed of a new definition from an existing one for the "copy" flow.
///
/// This is deliberately not <see cref="NodeSchema.Clone"/> with default results: a copy that
/// becomes a new type must not inherit the source's namespace children, loaded state or C# type
/// binding. The name is cleared so the user has to provide one, and the host-owned marker is
/// removed so the copy is an ordinary, editable definition.
/// </summary>
public static class SchemaDefinitionCopy
{
    /// <summary>
    /// Copies the definition fields of <paramref name="source"/> and drops its identity.
    /// The namespace is kept, so the target is a new type in the same namespace.
    /// </summary>
    public static NodeSchema Create(NodeSchema source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // withNamespaces=false: a namespace copy must not drag the source namespace's Schemas
        // into the new definition.
        NodeSchema copy = source.Clone(withNamespaces: false);

        // Identity of the source must not leak into the new definition: the copy has no name,
        // no namespace children, no loaded state and no C# type binding. Clone does not copy
        // Error, UsedBy, Compatibles or Provider, so those stay null on their own.
        copy.Name = string.Empty;
        copy.Schemas = null;
        copy.LoadState = null;
        copy.Type = null;

        // The copy is user-owned: keeping the marker would lock every field and every row action
        // that Man's system-defined rule applies to the source type.
        copy.ClearProperty<SystemDefined>();

        return copy;
    }
}
