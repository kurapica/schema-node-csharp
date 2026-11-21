using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.WPF.Man.Services;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Read-only row of the "Type definitions" list.
///
/// The row deliberately holds a plain <see cref="NodeSchema"/> instead of a DataNodeWrapper:
/// NodeSchema is a PropertyOwner (not a value node), so the view library's TableView — which walks
/// a wrapper's children — cannot be used here. The list therefore renders a plain read-only grid.
/// All display text is resolved by the caller on the thread pool, because LocaleString formatting
/// may resolve schema paths and trigger Core type loading.
/// </summary>
public sealed class TypeSchemaRowViewModel
{
    public TypeSchemaRowViewModel(NodeSchema schema, string displayText)
    {
        Schema = schema;
        Name = schema.Name;
        DisplayText = string.IsNullOrWhiteSpace(displayText) ? schema.Name : displayText;
        Kind = schema.Kind ?? string.Empty;
    }

    /// <summary>The underlying definition; used for navigation and deletion.</summary>
    public NodeSchema Schema { get; }

    /// <summary>Type name.</summary>
    public string Name { get; }

    /// <summary>Localized display text.</summary>
    public string DisplayText { get; }

    /// <summary>Schema kind (for example <c>system.int</c> or <c>namespace</c>).</summary>
    public string Kind { get; }

    /// <summary>Full name (<c>namespace.name</c>).</summary>
    public string FullName => Schema.FullName;

    /// <summary>Whether this row is a child namespace, which enables the drill-down action.</summary>
    public bool IsNamespace => Kind.Equals(NODE_KIND_NAMESPACE, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this row is a type definition; used to decide the plain "view" action.
    /// </summary>
    public bool IsType => !IsNamespace;

    /// <summary>Whether other definitions reference this one; a used definition cannot be deleted.</summary>
    public bool IsUsed => Schema.UsedBy is { Length: > 0 };

    /// <summary>
    /// Whether the schema is owned by the host code. Mirrors schema.vue's <c>SystemDefined</c>
    /// check: such definitions are never deletable, and their editability is decided by
    /// <see cref="SchemaDefinitionEditRule"/> (view-only today, except namespace kinds for
    /// permissions).
    /// </summary>
    public bool IsSystemDefined => Schema.GetProperty<SystemDefined>() is { Value: true };

    /// <summary>
    /// Mirrors <c>isSchemaUpdatable</c> from schema.vue: an explicit <c>schemaUpdate === false</c>
    /// disables editing.
    /// </summary>
    public bool CanUpdate => Schema.GetProperty<SchemaUpdate>() is not { Value: false };

    /// <summary>
    /// Independent edit switch for this row: the definition may be opened in the editor when Core
    /// allows updates and Man's own rule accepts the definition. That rule keeps a system-defined
    /// <em>namespace</em> editable (permissions are attached there) and locks every other
    /// system-defined kind; system kinds can be opened up later by changing that one rule. A
    /// namespace row therefore shows both "Child" and "Edit": the drill-down and the definition
    /// editor are independent actions.
    /// </summary>
    public bool IsEditable => CanUpdate && SchemaDefinitionEditRule.IsDefinitionEditable(Schema);

    /// <summary>
    /// Mirrors <c>isSchemaDeletable</c> from schema.vue: system-defined, referenced and
    /// delete-forbidden schemas are not deletable. The Core API still performs the final check.
    /// </summary>
    public bool CanDelete =>
        !IsSystemDefined
        && !IsUsed
        && Schema.GetProperty<SchemaDelete>() is not { Value: false };

    /// <summary>
    /// Demo is offered for the kinds that schema.vue exposes through "try it" (struct and array).
    /// The demo entry no longer exists in the shell navigation; it lives on the type's own row.
    /// </summary>
    public bool IsDemoable =>
        Kind.Equals(NODE_KIND_STRUCT, StringComparison.OrdinalIgnoreCase)
        || Kind.Equals(NODE_KIND_ARRAY, StringComparison.OrdinalIgnoreCase);
}
