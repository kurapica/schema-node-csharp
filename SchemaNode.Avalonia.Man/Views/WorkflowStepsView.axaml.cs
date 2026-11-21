using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SchemaNode.Avalonia.Man.Views;

/// <summary>Summary rendering of the workflow step list; editing stays on the workflow canvas.</summary>
public partial class WorkflowStepsView : UserControl
{
    public WorkflowStepsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
