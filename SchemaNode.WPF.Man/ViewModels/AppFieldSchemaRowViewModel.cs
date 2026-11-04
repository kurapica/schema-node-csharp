using SchemaNode.Property.App;
using SchemaNode.Schema;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Read-only row of the field list for one app, mirroring the field rows in
/// <c>appSchema.vue</c>: view / edit / try-it / move-up / delete with the Core-authored flags.
/// </summary>
public sealed class AppFieldSchemaRowViewModel
{
    public AppFieldSchemaRowViewModel(AppFieldSchema schema, string displayText, int index)
    {
        Schema = schema;
        Name = schema.Name;
        TypeText = schema.Type;
        Seqno = schema.Seqno;
        DisplayText = string.IsNullOrWhiteSpace(displayText) ? schema.Name : displayText;
        Index = index;

        CanView = schema.GetProperty<SchemaRead>()?.Value != false;
        CanEdit = schema.GetProperty<SchemaUpdate>()?.Value == true;
        CanDelete = schema.GetProperty<SchemaDelete>()?.Value == true;
        CanMoveUp = index > 0 && CanEdit;
    }

    public AppFieldSchema Schema { get; }
    public string Name { get; }
    public string TypeText { get; }
    public int Seqno { get; }
    public string DisplayText { get; }

    /// <summary>Zero-based position in the ordered field list (used for move-up).</summary>
    public int Index { get; }

    public bool CanView { get; }
    public bool CanEdit { get; }
    public bool CanDelete { get; }

    /// <summary>Move-up is offered only when the field is editable and not the first row.</summary>
    public bool CanMoveUp { get; }
}
