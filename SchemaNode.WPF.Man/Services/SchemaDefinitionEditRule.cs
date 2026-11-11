using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.WPF.Man.Services;

/// <summary>
/// Man's own rule for editable schema definitions.
///
/// This is a UI rule of the Man screens only: it is not a storage or save gate and has nothing to
/// do with the schema properties themselves, so it lives in the Man layer instead of
/// <c>SchemaNode.App</c>. The underlying runtime and storage keep full write capability for every
/// definition; this rule only decides which edit entry the screens offer and which fields they
/// render as read-only.
/// </summary>
public static class SchemaDefinitionEditRule
{
    /// <summary>The permission field that stays editable on a system-defined namespace.</summary>
    public const string AuthsFieldName = "auths";

    /// <summary>
    /// The single switch for editing host-owned definitions. Off by default: system-defined
    /// definitions are view-only, except namespace kinds whose <c>auths</c> field stays editable so
    /// permissions can be configured. Turn it on to treat system definitions exactly like ordinary
    /// ones; nothing in Core or App has to change.
    /// </summary>
    public static bool AllowSystemDefinedEditing { get; set; }

    /// <summary>Whether the definition may be edited at all. A null schema means a new/copied definition.</summary>
    public static bool IsDefinitionEditable(NodeSchema? schema)
        => schema is null || AllowSystemDefinedEditing || !IsSystemDefined(schema) || IsNamespaceKind(schema);

    /// <summary>Whether a single definition field accepts edits.</summary>
    public static bool IsFieldEditable(NodeSchema? schema, string fieldName)
        => schema is null
           || AllowSystemDefinedEditing
           || !IsSystemDefined(schema)
           || (IsNamespaceKind(schema)
               && fieldName.Equals(AuthsFieldName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the definition is owned by the host code.</summary>
    public static bool IsSystemDefined(NodeSchema schema)
        => schema.GetProperty<SystemDefined>() is { Value: true };

    /// <summary>Whether the definition is a namespace (the only system-defined kind that keeps an editable field).</summary>
    public static bool IsNamespaceKind(NodeSchema schema)
        => (schema.Kind ?? string.Empty).Equals(NODE_KIND_NAMESPACE, StringComparison.OrdinalIgnoreCase);
}
