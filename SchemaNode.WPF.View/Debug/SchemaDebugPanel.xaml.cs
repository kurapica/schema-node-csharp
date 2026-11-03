using System.Windows.Controls;

namespace SchemaNode.WPF.View.Debug;

/// <summary>
/// Debug-only inspector body. It is a plain <see cref="UserControl"/> bound to a
/// <see cref="DebugNodeSnapshot"/>; the button that opens it is created only in DEBUG builds.
/// </summary>
public partial class SchemaDebugPanel : UserControl
{
    /// <summary>Creates the panel.</summary>
    public SchemaDebugPanel()
    {
        InitializeComponent();
    }

    /// <summary>Shows the snapshot for <paramref name="node"/>.</summary>
    public void Show(DebugNodeSnapshot snapshot) => DataContext = snapshot;
}
