using System.Windows.Controls;

namespace SchemaNode.WPF.View.Debug;

/// <summary>
/// Inspector body. It is a plain <see cref="UserControl"/> bound to a
/// <see cref="DebugNodeSnapshot"/>; the button that opens it exists only while the inheritable
/// <c>SchemaView.Debug</c> property is on, so the type is compiled in every configuration but never
/// materialized unless a host asks for it.
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
