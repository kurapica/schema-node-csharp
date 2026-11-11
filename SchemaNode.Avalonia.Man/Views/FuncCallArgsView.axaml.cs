using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SchemaNode.Avalonia.Man.Views;

/// <summary>Man-only projection of the Core-owned function call argument list.</summary>
public partial class FuncCallArgsView : UserControl
{
    public FuncCallArgsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
