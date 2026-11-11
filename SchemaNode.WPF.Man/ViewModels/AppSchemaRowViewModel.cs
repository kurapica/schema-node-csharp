using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Schema;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Read-only row of the "Apps" list.
///
/// The visibility rules mirror <c>appSchema.vue</c> exactly:
///   - drill into child apps only when the app has no Fields;
///   - open the field list only when the app has no sub apps;
///   - expose the workflow action only when the app has Fields;
///   - allow delete only when it has no sub apps, no fields and no workflows (and the schema grants delete).
/// </summary>
public sealed class AppSchemaRowViewModel
{
    public AppSchemaRowViewModel(
        AppSchema schema,
        string displayText,
        bool hasFields,
        bool hasApps,
        bool hasWorkflows)
    {
        Schema = schema;
        Name = schema.Name;
        DisplayText = string.IsNullOrWhiteSpace(displayText) ? schema.Name : displayText;
        HasFields = hasFields;
        HasApps = hasApps;
        HasWorkflows = hasWorkflows;

        CanView = schema.GetProperty<SchemaRead>()?.Value != false;
        CanEdit = schema.GetProperty<SchemaUpdate>()?.Value == true;
        // System-defined apps are owned by the host code and must never be deleted; the
        // remaining leaf/no-workflow rule is the appSchema.vue delete predicate.
        CanDelete = !IsSystemDefined && !hasApps && !hasFields && !hasWorkflows
                    && schema.GetProperty<SchemaDelete>()?.Value == true;
    }

    /// <summary>Whether the app is defined by the host code rather than by the user.</summary>
    public bool IsSystemDefined => Schema.GetProperty<SystemDefined>() is { Value: true };

    /// <summary>The underlying definition; used for navigation and deletion.</summary>
    public AppSchema Schema { get; }

    /// <summary>Short app name.</summary>
    public string Name { get; }

    /// <summary>Localized display text.</summary>
    public string DisplayText { get; }

    /// <summary>Full app name (container + name).</summary>
    public string FullName => Schema.FullName;

    /// <summary>Whether the app declares Fields.</summary>
    public bool HasFields { get; }

    /// <summary>Whether the app has child apps.</summary>
    public bool HasApps { get; }

    /// <summary>Whether the app declares workflows.</summary>
    public bool HasWorkflows { get; }

    /// <summary>Whether the row can be opened read-only.</summary>
    public bool CanView { get; }

    /// <summary>Whether the row can be opened for editing.</summary>
    public bool CanEdit { get; }

    /// <summary>Drill into child apps is offered only when the app has no Fields.</summary>
    public bool CanDrill => !HasFields;

    /// <summary>The field list is offered only when the app has no child apps.</summary>
    public bool CanShowFields => !HasApps;

    /// <summary>The workflow action is offered only when the app has Fields.</summary>
    public bool CanShowWorkflow => HasFields;

    /// <summary>Deletion is allowed only for leaf apps without fields or workflows.</summary>
    public bool CanDelete { get; }
}
