using SchemaNode.Property.App;
using SchemaNode.Schema;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Read-only row of the workflow list for one app. The active state controls both the toggle action
/// and whether the workflow can be edited/deleted (Core refuses writes to an activated workflow).
/// </summary>
public sealed class AppWorkflowSchemaRowViewModel
{
    public AppWorkflowSchemaRowViewModel(AppWorkflowSchema schema, string displayText, string descriptionText)
    {
        Schema = schema;
        Name = schema.Name;
        DisplayText = string.IsNullOrWhiteSpace(displayText) ? schema.Name : displayText;
        DescriptionText = descriptionText;
        Active = schema.Active;

        CanEdit = schema.GetProperty<SchemaUpdate>()?.Value == true;
        CanDelete = schema.GetProperty<SchemaDelete>()?.Value == true && !schema.Active;
    }

    public AppWorkflowSchema Schema { get; }
    public string Name { get; }
    public string DisplayText { get; }
    public string DescriptionText { get; }
    public bool Active { get; }

    /// <summary>Viewing is always allowed.</summary>
    public bool CanView => true;

    public bool CanEdit { get; }

    /// <summary>An activated workflow cannot be deleted (Core returns false).</summary>
    public bool CanDelete { get; }
}
